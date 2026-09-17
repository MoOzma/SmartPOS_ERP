using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Security;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrderController> _logger;
        private readonly StockLedgerService _stockLedger;

        public OrderController(ApplicationDbContext context, ILogger<OrderController> logger, StockLedgerService stockLedger)
        {
            _context = context;
            _logger = logger;
            _stockLedger = stockLedger;
        }

        public async Task<IActionResult> Index(int? invoiceId, DateTime? from, DateTime? to, int page = 1)
        {
            const int pageSize = 20;
            var query = VisibleOrders().Include(o => o.OrderDetails).AsQueryable();

            if (invoiceId.HasValue && invoiceId.Value > 0)
            {
                query = query.Where(o => o.Id == invoiceId.Value);
            }

            if (from.HasValue)
            {
                query = query.Where(o => o.OrderDate >= from.Value.Date);
            }

            if (to.HasValue)
            {
                query = query.Where(o => o.OrderDate < to.Value.Date.AddDays(1));
            }

            var totalCount = await query.CountAsync();
            page = Math.Max(1, page);
            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.InvoiceId = invoiceId;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            ViewBag.Page = page;
            ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            ViewBag.TotalCount = totalCount;

            return View(orders);
        }

        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await VisibleOrders()
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }

        public async Task<IActionResult> Receipt(int id)
        {
            var order = await VisibleOrders()
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null) return NotFound();

            var printEnabled = await _context.StoreSettings
                .AnyAsync(s => s.Id == StoreSettingsService.SingletonId && s.PrintReceiptAfterSale);
            if (!printEnabled)
            {
                return RedirectToAction(nameof(OrderDetails), new { id });
            }

            ViewData["Title"] = $"إيصال #{order.Id}";
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessReturn(int orderId, int productId, decimal returnQty, string? reason = null, string? notes = null)
        {
            if (!AppPermissions.Has(User, AppPermissions.Return))
            {
                return Json(new { success = false, message = "غير مصرح لك بتنفيذ المرتجع." });
            }

            var reasonError = ReturnReasons.Validate(reason, notes);
            if (reasonError != null)
            {
                return Json(new { success = false, message = reasonError });
            }

            var (normalizedReason, normalizedNotes) = ReturnReasons.Normalize(reason, notes);

            var shiftId = await _context.GetOpenShiftIdAsync(User.Identity?.Name);
            if (shiftId is null)
            {
                return Json(new { success = false, message = "يجب فتح وردية أولاً قبل تسجيل مرتجع." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = await VisibleOrders()
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                var orderDetail = order?.OrderDetails
                    .FirstOrDefault(d => d.ProductId == productId);

                if (order == null)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "الفاتورة غير موجودة أو غير مسموحة." });
                }

                if (orderDetail == null)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "الصنف غير موجود في الفاتورة." });
                }

                var product = await _context.GetProductWithRowLockAsync(productId);
                if (product == null)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "المنتج غير موجود." });
                }

                var qtyError = ReturnQuantity.Validate(product.Unit, returnQty, orderDetail.Quantity);
                if (qtyError != null)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = qtyError });
                }

                var lineRefund = returnQty * orderDetail.UnitPrice;
                var taxRefund = Math.Round(lineRefund * (orderDetail.TaxRate / 100m), 2);
                var refundAmount = lineRefund + taxRefund;

                var ledgerNote = $"فاتورة بيع #{orderId}";
                if (normalizedReason != null)
                {
                    ledgerNote += $" — {normalizedReason}";
                }

                if (product.TrackInventory)
                {
                    _stockLedger.ApplyDelta(
                        product,
                        returnQty,
                        StockMovementTypes.Return,
                        "مرتجع بيع",
                        ledgerNote);
                }

                orderDetail.Quantity -= returnQty;
                order.TotalAmount -= refundAmount;
                order.TaxAmount -= taxRefund;
                if (order.TotalAmount < 0)
                {
                    order.TotalAmount = 0;
                }
                if (order.TaxAmount < 0)
                {
                    order.TaxAmount = 0;
                }

                _context.SalesReturns.Add(new SalesReturn
                {
                    OrderId = orderId,
                    ProductId = productId,
                    Quantity = returnQty,
                    RefundAmount = refundAmount,
                    Reason = normalizedReason,
                    Notes = normalizedNotes,
                    ReturnDate = DateTime.Now,
                    ShiftId = shiftId
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new { success = true, message = "تمت عملية الارتجاع وتحديث المخزن والفاتورة بنجاح" });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "Concurrency conflict while processing return");
                return Json(new { success = false, message = "تم تعديل المخزون من عملية أخرى. أعد المحاولة." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to process sales return");
                return Json(new { success = false, message = "حدث خطأ أثناء الحفظ. حاول مرة أخرى." });
            }
        }

        private IQueryable<Order> VisibleOrders()
        {
            var query = _context.Orders.AsQueryable();
            if (AppPermissions.Has(User, AppPermissions.AllOrders))
            {
                return query;
            }

            var userId = CurrentUserId();
            if (userId is null)
            {
                return query.Where(_ => false);
            }

            return query.Where(o => o.Shift != null && o.Shift.UserId == userId);
        }

        private int? CurrentUserId()
        {
            var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(value, out var id) ? id : null;
        }
    }
}
