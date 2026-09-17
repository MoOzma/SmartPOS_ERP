using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Security;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Controllers
{
    [Authorize(Policy = AppPermissions.Purchases)]
    public class PurchasesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PurchasesController> _logger;
        private readonly StockLedgerService _stockLedger;
        private readonly SupplierDirectoryService _suppliers;
        private readonly PurchaseInvoiceReportService _purchaseReports;

        public PurchasesController(
            ApplicationDbContext context,
            ILogger<PurchasesController> logger,
            StockLedgerService stockLedger,
            SupplierDirectoryService suppliers,
            PurchaseInvoiceReportService purchaseReports)
        {
            _context = context;
            _logger = logger;
            _stockLedger = stockLedger;
            _suppliers = suppliers;
            _purchaseReports = purchaseReports;
        }


        public async Task<IActionResult> Index(
            string? period,
            DateTime? selectedDate,
            int? selectedMonth,
            int? selectedYear,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? q = null,
            CancellationToken cancellationToken = default)
        {
            var model = await _purchaseReports.BuildAsync(
                period,
                selectedDate,
                selectedMonth,
                selectedYear,
                DateTime.Now,
                fromDate,
                toDate,
                q,
                cancellationToken);
            return View(model);
        }

        // 2. عرض تفاصيل فاتورة واحدة
        public async Task<IActionResult> Details(int id)
        {
            var invoice = await _context.PurchaseInvoices
                .Include(i => i.Supplier)
                .Include(i => i.Details)
                    .ThenInclude(d => d.Product)
                .Include(i => i.Returns)
                    .ThenInclude(r => r.Details)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null) return NotFound();

            return View(invoice);
        }


        public async Task<IActionResult> Create()
        {
            if (await _context.GetOpenShiftIdAsync(User.Identity?.Name) is null)
            {
                TempData["Error"] = "يجب فتح وردية أولاً قبل تسجيل فاتورة توريد.";
                return RedirectToAction("Open", "Shifts");
            }

            ViewBag.Suppliers = await _context.Suppliers.ToListAsync();
            ViewBag.Products = await _context.Products.ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePurchase([FromBody] PurchaseViewModel model)
        {
            if (model == null || model.Items == null || !model.Items.Any()) return BadRequest("بيانات الفاتورة فارغة");

            if (await _context.GetOpenShiftIdAsync(User.Identity?.Name) is null)
            {
                return BadRequest(new { message = "يجب فتح وردية أولاً قبل تسجيل فاتورة توريد." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var invoice = new PurchaseInvoice
                {
                    SupplierId = model.SupplierId,
                    InvoiceDate = model.PurchaseDate == default ? DateTime.Now : model.PurchaseDate,
                    Details = new List<PurchaseDetail>()
                };

                var lines = new List<(Product Product, PurchaseItemViewModel Item, decimal Units, decimal IncomingUnitCost, decimal PackageCost)>();

                foreach (var item in model.Items)
                {
                    var lineError = InputRules.PurchaseLine(item);
                    if (lineError != null)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { message = lineError });
                    }

                    var product = await _context.GetProductWithRowLockAsync(item.ProductId);
                    if (product == null)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest("أحد الأصناف غير موجود");
                    }

                    decimal totalNewUnits = item.PackageQuantity * item.UnitsPerPackage;
                    var incomingUnitCost = item.PackageCost is decimal packageCost
                        ? packageCost / item.UnitsPerPackage
                        : product.CostPrice;
                    var storedPackageCost = item.PackageCost ?? product.CostPrice * item.UnitsPerPackage;

                    invoice.Details.Add(new PurchaseDetail
                    {
                        ProductId = product.Id,
                        PackageQuantity = item.PackageQuantity,
                        UnitsPerPackage = item.UnitsPerPackage,
                        TotalUnits = totalNewUnits,
                        PackageCost = storedPackageCost,
                        UnitCost = incomingUnitCost
                    });
                    lines.Add((product, item, totalNewUnits, incomingUnitCost, storedPackageCost));
                }

                _context.PurchaseInvoices.Add(invoice);
                await _context.SaveChangesAsync();

                foreach (var (product, item, units, incomingUnitCost, _) in lines)
                {
                    var oldStock = product.StockQuantity;
                    if (oldStock + units > 0)
                    {
                        product.CostPrice = Math.Round(
                            (oldStock * product.CostPrice + units * incomingUnitCost) / (oldStock + units),
                            2);
                    }
                    else
                    {
                        product.CostPrice = incomingUnitCost;
                    }

                    if (item.NewSalePrice is > 0)
                    {
                        product.SalePrice = item.NewSalePrice.Value;
                    }

                    _stockLedger.ApplyDelta(product, units, StockMovementTypes.Purchase, "توريد", $"فاتورة توريد #{invoice.Id}");
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "تم تسجيل المشتريات وتحديث المخزون بنجاح", id = invoice.Id });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "Concurrency conflict while saving purchase");
                return Conflict(new { message = "تم تعديل المخزون من عملية أخرى. أعد المحاولة." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to save purchase");
                return StatusCode(500, new { message = "حدث خطأ أثناء الحفظ. حاول مرة أخرى." });
            }
        }


        public IActionResult CreateSupplier()
        {
            return View(new Supplier { Invoices = [], Payments = [] });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSupplier([Bind("Name,Phone,Address,Notes")] Supplier supplier)
        {
            if (string.IsNullOrWhiteSpace(supplier.Name))
            {
                ModelState.AddModelError(nameof(supplier.Name), "اسم المورد مطلوب");
            }

            if (!ModelState.IsValid)
            {
                return View(supplier);
            }

            supplier.Name = supplier.Name.Trim();
            supplier.Invoices ??= [];
            supplier.Payments ??= [];
            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Suppliers));
        }

        public async Task<IActionResult> EditSupplier(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            return supplier == null ? NotFound() : View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSupplier(int id, [Bind("Id,Name,Phone,Address,Notes")] Supplier supplier)
        {
            if (id != supplier.Id)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(supplier.Name))
            {
                ModelState.AddModelError(nameof(supplier.Name), "اسم المورد مطلوب");
            }

            if (!ModelState.IsValid)
            {
                return View(supplier);
            }

            var existing = await _context.Suppliers.FindAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            existing.Name = supplier.Name.Trim();
            existing.Phone = supplier.Phone;
            existing.Address = supplier.Address;
            existing.Notes = supplier.Notes;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Suppliers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoidPurchase(int id)
        {
            if (await _context.GetOpenShiftIdAsync(User.Identity?.Name) is null)
            {
                TempData["Error"] = "يجب فتح وردية أولاً قبل إلغاء فاتورة توريد.";
                return RedirectToAction("Open", "Shifts");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var invoice = await _context.PurchaseInvoices
                    .Include(i => i.Details)
                    .FirstOrDefaultAsync(i => i.Id == id);
                if (invoice == null)
                {
                    return NotFound();
                }

                if (invoice.IsVoided)
                {
                    TempData["Error"] = "هذه الفاتورة ملغاة مسبقاً.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                foreach (var detail in invoice.Details)
                {
                    var product = await _context.GetProductWithRowLockAsync(detail.ProductId);
                    if (product == null || !product.TrackInventory)
                    {
                        continue;
                    }

                    _stockLedger.ApplyDelta(
                        product,
                        -detail.TotalUnits,
                        StockMovementTypes.Purchase,
                        "إلغاء فاتورة توريد",
                        $"إلغاء فاتورة توريد #{invoice.Id}");
                }

                invoice.IsVoided = true;
                invoice.VoidedAt = DateTime.Now;
                invoice.VoidedBy = User.Identity?.Name;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                TempData["Success"] = "تم إلغاء فاتورة التوريد وإرجاع الكميات للمخزن.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to void purchase {InvoiceId}", id);
                TempData["Error"] = "تعذر إلغاء الفاتورة.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuickSupplier([FromBody] Supplier supplier)
        {
            if (string.IsNullOrEmpty(supplier.Name)) return BadRequest();

            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();

            // نعيد البيانات للـ View لتحديث القائمة فوراً
            return Json(new { id = supplier.Id, name = supplier.Name });
        }


        // 1. أكشن لعرض كشف حساب مورد محدد (أحمد مثلاً)
        public async Task<IActionResult> SupplierAccount(int id)
        {
            var supplier = await _context.Suppliers
                .Include(s => s.Invoices)
                    .ThenInclude(i => i.Details)
                .Include(s => s.Payments)
                .Include(s => s.Returns)
                    .ThenInclude(r => r.Details)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supplier == null) return NotFound();

            return View(supplier);
        }

        // 2. أكشن لتسجيل عملية دفع مالي للمورد (صرف مبلغ)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PaySupplier(int supplierId, decimal amount, DateTime paymentDate, string? notes, bool fromCash = true)
        {
            if (amount <= 0) return BadRequest(InputRules.AmountMustBePositive);

            int? shiftId = null;
            if (fromCash)
            {
                shiftId = await _context.GetOpenShiftIdAsync(User.Identity?.Name);
                if (shiftId is null)
                {
                    TempData["Error"] = "يجب فتح وردية أولاً لتسجيل دفع من الصندوق، أو ألغِ خيار الصندوق.";
                    return RedirectToAction("Open", "Shifts");
                }
            }

            var payment = new SupplierPayment
            {
                SupplierId = supplierId,
                AmountPaid = amount,
                PaymentDate = paymentDate,
                Notes = notes ?? "بدون ملاحظات",
                FromCash = fromCash,
                ShiftId = shiftId
            };

            _context.SupplierPayments.Add(payment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(SupplierAccount), new { id = supplierId });
        }

        public async Task<IActionResult> Return(int id)
        {
            if (await _context.GetOpenShiftIdAsync(User.Identity?.Name) is null)
            {
                TempData["Error"] = "يجب فتح وردية أولاً قبل مرتجع التوريد.";
                return RedirectToAction("Open", "Shifts");
            }

            var invoice = await LoadInvoiceForReturnAsync(id);
            if (invoice == null) return NotFound();
            if (invoice.IsVoided)
            {
                TempData["Error"] = "لا يمكن عمل مرتجع على فاتورة ملغاة.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewBag.Remaining = RemainingByDetail(invoice);
            return View(invoice);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnPurchase(int id, PurchaseReturnForm form)
        {
            if (await _context.GetOpenShiftIdAsync(User.Identity?.Name) is null)
            {
                TempData["Error"] = "يجب فتح وردية أولاً قبل مرتجع التوريد.";
                return RedirectToAction("Open", "Shifts");
            }

            var invoice = await LoadInvoiceForReturnAsync(id);
            if (invoice == null) return NotFound();
            if (invoice.IsVoided)
            {
                TempData["Error"] = "لا يمكن عمل مرتجع على فاتورة ملغاة.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var remaining = RemainingByDetail(invoice);
            if ((form.Lines ?? []).Any(l => l.Quantity < 0))
            {
                TempData["Error"] = InputRules.QuantityCannotBeNegative;
                ViewBag.Remaining = remaining;
                return View("Return", invoice);
            }

            var selected = (form.Lines ?? [])
                .Where(l => l.Quantity > 0)
                .ToList();
            if (selected.Count == 0)
            {
                TempData["Error"] = "حدد كمية مرتجعة لصنف واحد على الأقل.";
                ViewBag.Remaining = remaining;
                return View("Return", invoice);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var purchaseReturn = new PurchaseReturn
                {
                    SupplierId = invoice.SupplierId,
                    PurchaseInvoiceId = invoice.Id,
                    ReturnDate = DateTime.Now,
                    ShiftId = await _context.GetOpenShiftIdAsync(User.Identity?.Name),
                    UserName = User.Identity?.Name,
                    Notes = form.Notes
                };

                foreach (var line in selected)
                {
                    var detail = invoice.Details.FirstOrDefault(d => d.Id == line.PurchaseDetailId);
                    if (detail == null)
                    {
                        await transaction.RollbackAsync();
                        TempData["Error"] = "سطر التوريد غير موجود.";
                        return RedirectToAction(nameof(Return), new { id });
                    }

                    remaining.TryGetValue(detail.Id, out var left);
                    if (line.Quantity > left)
                    {
                        await transaction.RollbackAsync();
                        TempData["Error"] = $"الكمية المرتجعة أكبر من المتبقي لصنف {detail.Product?.Name}.";
                        return RedirectToAction(nameof(Return), new { id });
                    }

                    var product = await _context.GetProductWithRowLockAsync(detail.ProductId);
                    if (product != null && product.TrackInventory && product.StockQuantity < line.Quantity)
                    {
                        await transaction.RollbackAsync();
                        TempData["Error"] = $"المخزن لا يكفي لمرتجع {product.Name}.";
                        return RedirectToAction(nameof(Return), new { id });
                    }

                    purchaseReturn.Details.Add(new PurchaseReturnDetail
                    {
                        PurchaseDetailId = detail.Id,
                        ProductId = detail.ProductId,
                        Quantity = line.Quantity,
                        UnitCost = detail.UnitCost,
                        LineTotal = Math.Round(line.Quantity * detail.UnitCost, 2)
                    });

                    if (product is { TrackInventory: true })
                    {
                        _stockLedger.ApplyDelta(
                            product,
                            -line.Quantity,
                            StockMovementTypes.PurchaseReturn,
                            "مرتجع توريد",
                            $"مرتجع توريد فاتورة #{invoice.Id}");
                    }
                }

                _context.PurchaseReturns.Add(purchaseReturn);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                TempData["Success"] = "تم تسجيل مرتجع التوريد وإنقاص المخزون والمديونية.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed purchase return {InvoiceId}", id);
                TempData["Error"] = "تعذر تسجيل مرتجع التوريد.";
                return RedirectToAction(nameof(Return), new { id });
            }
        }

        private async Task<PurchaseInvoice?> LoadInvoiceForReturnAsync(int id)
        {
            return await _context.PurchaseInvoices
                .Include(i => i.Supplier)
                .Include(i => i.Details)
                    .ThenInclude(d => d.Product)
                .Include(i => i.Returns)
                    .ThenInclude(r => r.Details)
                .FirstOrDefaultAsync(i => i.Id == id);
        }

        private static Dictionary<int, decimal> RemainingByDetail(PurchaseInvoice invoice)
        {
            var returned = (invoice.Returns ?? [])
                .SelectMany(r => r.Details ?? [])
                .GroupBy(d => d.PurchaseDetailId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            return invoice.Details.ToDictionary(
                d => d.Id,
                d => d.TotalUnits - (returned.TryGetValue(d.Id, out var qty) ? qty : 0));
        }

        public async Task<IActionResult> Suppliers(string? q, string? status, CancellationToken cancellationToken = default)
        {
            var model = await _suppliers.BuildAsync(q, status, cancellationToken);
            return View(model);
        }
    }
}