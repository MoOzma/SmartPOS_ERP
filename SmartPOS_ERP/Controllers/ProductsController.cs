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
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProductsController> _logger;
        private readonly StockLedgerService _stockLedger;
        private readonly StoreSettingsService _storeSettings;

        public ProductsController(
            ApplicationDbContext context,
            ILogger<ProductsController> logger,
            StockLedgerService stockLedger,
            StoreSettingsService storeSettings)
        {
            _context = context;
            _logger = logger;
            _stockLedger = stockLedger;
            _storeSettings = storeSettings;
        }

        // GET: Products
        public async Task<IActionResult> Index()
        {
            ViewBag.OpenShiftId = await _context.GetOpenShiftIdAsync(User.Identity?.Name);
            ViewBag.OpenCreditCount = await _context.CreditInvoices.CountAsync(i => i.Status == CreditInvoiceStatuses.Open);
            ViewData["HideFooter"] = true;
            return View(await _context.Products
                .OrderByDescending(p => p.IsPinned)
                .ThenBy(p => p.Name)
                .ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePin(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            product.IsPinned = !product.IsPinned;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isPinned = product.IsPinned });
        }

        // GET: Products/Details/5
        [Authorize(Policy = AppPermissions.Products)]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(m => m.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        [Authorize(Policy = AppPermissions.Products)]
        public async Task<IActionResult> Create()
        {
            await LoadCategoriesAsync();
            return View(new Product { Unit = "Piece", TrackInventory = true });
        }

      
        [HttpPost]
        [Authorize(Policy = AppPermissions.Products)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Barcode,CostPrice,SalePrice,TaxRate,UseCustomTax,TrackInventory,StockQuantity,Unit,ReorderLevel,ImagePath,Category,ExpiryDate,CartonCount,CartonPrice,PiecesPerCarton")] Product product)
        {
            NormalizeProduct(product);
            ModelState.Remove(nameof(Product.RowVersion));
            PrepareRowVersionForInsert(product);
            InputRules.ApplyProduct(ModelState, product);
            if (string.Equals(product.Unit, "Piece", StringComparison.OrdinalIgnoreCase))
            {
                InputRules.ApplyPieceCarton(ModelState, product);
            }
            if (await NameTakenAsync(product.Name, 0))
            {
                InputRules.AddError(ModelState, nameof(product.Name), InputRules.NameTaken);
            }
            if (await BarcodeTakenAsync(product.Barcode, 0))
            {
                InputRules.AddError(ModelState, nameof(product.Barcode), InputRules.BarcodeTaken);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    product.Category = ProductCategories.Normalize(product.Category);
                    var openingQty = product.StockQuantity;
                    _context.Add(product);
                    await _context.SaveChangesAsync();
                    if (openingQty != 0)
                    {
                        _stockLedger.RecordSnapshot(product.Id, 0, openingQty, StockMovementTypes.Opening, "رصيد أول المدة", $"منتج #{product.Id}");
                        await _context.SaveChangesAsync();
                    }
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogWarning(ex, "Failed to create product {ProductName}", product.Name);
                    if (!string.IsNullOrEmpty(product.Barcode) && await BarcodeTakenAsync(product.Barcode, product.Id))
                    {
                        InputRules.AddError(ModelState, nameof(product.Barcode), InputRules.BarcodeTaken);
                    }
                    else if (await NameTakenAsync(product.Name, product.Id))
                    {
                        InputRules.AddError(ModelState, nameof(product.Name), InputRules.NameTaken);
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "تعذر حفظ المنتج. راجع البيانات وحاول مرة أخرى.");
                    }
                }
            }
            await LoadCategoriesAsync();
            return View(product);
        }

        [Authorize(Policy = AppPermissions.Products)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            await LoadCategoriesAsync();
            return View(product);
        }

        [HttpPost]
        [Authorize(Policy = AppPermissions.Products)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Barcode,CostPrice,SalePrice,TaxRate,UseCustomTax,TrackInventory,StockQuantity,ReorderLevel,Unit,ImagePath,Category,ExpiryDate")] Product product)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            NormalizeProduct(product);
            ModelState.Remove(nameof(Product.RowVersion));
            InputRules.ApplyProduct(ModelState, product);
            if (await NameTakenAsync(product.Name, product.Id))
            {
                InputRules.AddError(ModelState, nameof(product.Name), InputRules.NameTaken);
            }
            if (await BarcodeTakenAsync(product.Barcode, product.Id))
            {
                InputRules.AddError(ModelState, nameof(product.Barcode), InputRules.BarcodeTaken);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Products.FindAsync(id);
                    if (existing == null)
                    {
                        return NotFound();
                    }

                    existing.Name = product.Name;
                    existing.Barcode = product.Barcode;
                    existing.CostPrice = product.CostPrice;
                    existing.SalePrice = product.SalePrice;
                    existing.TaxRate = product.TaxRate;
                    existing.UseCustomTax = product.UseCustomTax;
                    existing.TrackInventory = product.TrackInventory;
                    existing.ReorderLevel = product.ReorderLevel;
                    existing.Unit = product.Unit;
                    existing.ImagePath = product.ImagePath;
                    existing.Category = ProductCategories.Normalize(product.Category);
                    existing.ExpiryDate = product.ExpiryDate;

                    var stockDelta = product.StockQuantity - existing.StockQuantity;
                    if (stockDelta != 0)
                    {
                        _stockLedger.ApplyDelta(existing, stockDelta, StockMovementTypes.Adjustment, "تعديل يدوي", $"منتج #{existing.Id}");
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogWarning(ex, "Failed to update product {ProductId}", product.Id);
                    InputRules.AddError(ModelState, nameof(product.Barcode), InputRules.BarcodeTaken);
                    await LoadCategoriesAsync();
                    return View(product);
                }
                return RedirectToAction(nameof(Index));
            }
            await LoadCategoriesAsync();
            return View(product);
        }

        [Authorize(Policy = AppPermissions.Products)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(m => m.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize(Policy = AppPermissions.Products)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }

        private static void NormalizeProduct(Product product)
        {
            product.Name = string.IsNullOrWhiteSpace(product.Name) ? "" : product.Name.Trim();
            product.Barcode = string.IsNullOrWhiteSpace(product.Barcode) ? null : product.Barcode.Trim();
        }

        private void PrepareRowVersionForInsert(Product product)
        {
            if (_context.Database.IsSqlServer())
            {
                product.RowVersion = null!;
                return;
            }

            if (product.RowVersion is not { Length: > 0 })
            {
                product.RowVersion = new byte[] { 1 };
            }
        }

        private Task<bool> NameTakenAsync(string? name, int excludeProductId)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Task.FromResult(false);
            }

            return _context.Products.AnyAsync(p => p.Name == name && p.Id != excludeProductId);
        }

        private Task<bool> BarcodeTakenAsync(string? barcode, int excludeProductId)
        {
            if (string.IsNullOrEmpty(barcode))
            {
                return Task.FromResult(false);
            }

            return _context.Products.AnyAsync(p => p.Barcode == barcode && p.Id != excludeProductId);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveOrder([FromBody] OrderViewModel model)
        {
            if (model == null || model.OrderDetails == null || !model.OrderDetails.Any()) return BadRequest();

            var shiftId = await _context.GetOpenShiftIdAsync(User.Identity?.Name);
            if (shiftId is null)
            {
                return BadRequest(new { message = "يجب فتح وردية أولاً قبل تسجيل فاتورة بيع." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = new Order
                {
                    OrderDate = DateTime.Now,
                    TotalAmount = 0,
                    TaxAmount = 0,
                    OrderDetails = new List<OrderDetail>()
                };

                var stockLines = new List<(Product Product, decimal Quantity)>();
                decimal subtotal = 0;
                decimal taxAmount = 0;
                var settings = await _storeSettings.GetAsync();

                foreach (var item in model.OrderDetails)
                {
                    if (item.Quantity <= 0)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { message = InputRules.QuantityMustBePositive });
                    }

                    var product = await _context.GetProductWithRowLockAsync(item.ProductId);
                    if (product == null)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { message = "أحد الأصناف غير موجود." });
                    }

                    if (!InputRules.IsPositive(product.SalePrice))
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { message = InputRules.SalePriceMustBePositive });
                    }

                    if (product.TrackInventory)
                    {
                        if (product.StockQuantity < item.Quantity)
                        {
                            await transaction.RollbackAsync();
                            string unitName = product.Unit == "Kilo" ? "كجم" : "قطعة";
                            return BadRequest(new
                            {
                                message = $"خطأ في كمية {product.Name}: المطلوب ({item.Quantity} {unitName})، والمتاح في المخزن ({product.StockQuantity} {unitName}) فقط!"
                            });
                        }

                        stockLines.Add((product, item.Quantity));
                    }

                    var unitPrice = product.SalePrice;
                    var taxRate = StoreSettingsService.EffectiveTaxRate(product, settings);
                    order.OrderDetails.Add(new OrderDetail
                    {
                        ProductId = product.Id,
                        Quantity = item.Quantity,
                        UnitPrice = unitPrice,
                        UnitCost = product.CostPrice,
                        TaxRate = taxRate
                    });

                    var lineTotal = unitPrice * item.Quantity;
                    subtotal += lineTotal;
                    taxAmount += lineTotal * (taxRate / 100m);
                }

                order.TaxAmount = Math.Round(taxAmount, 2);
                var discount = Math.Round(model.DiscountAmount, 2);
                if (discount < 0)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { message = InputRules.DiscountCannotBeNegative });
                }

                if (discount > subtotal)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { message = "الخصم لا يمكن أن يتجاوز إجمالي الأصناف." });
                }

                if (discount > 0 && subtotal > 0)
                {
                    var factor = (subtotal - discount) / subtotal;
                    order.TaxAmount = Math.Round(order.TaxAmount * factor, 2);
                }

                order.DiscountAmount = discount;
                var paymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod)
                    ? PaymentMethods.FirstEnabled(settings)
                    : PaymentMethods.Normalize(model.PaymentMethod);
                if (!PaymentMethods.IsAllowed(paymentMethod, settings))
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { message = "طريقة الدفع غير مفعّلة في إعدادات النظام." });
                }

                order.PaymentMethod = paymentMethod;
                order.TotalAmount = subtotal - discount + order.TaxAmount;
                order.ShiftId = shiftId;

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var (product, quantity) in stockLines)
                {
                    _stockLedger.ApplyDelta(product, -quantity, StockMovementTypes.Sale, "بيع", $"فاتورة بيع #{order.Id}");
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (settings.PrintReceiptAfterSale)
                {
                    return Ok(new
                    {
                        message = "تمت العملية بنجاح",
                        orderId = order.Id,
                        receiptUrl = $"/Order/Receipt/{order.Id}"
                    });
                }

                return Ok(new { message = "تمت العملية بنجاح", orderId = order.Id });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "Concurrency conflict while saving order");
                return Conflict(new { message = "تم تعديل المخزون من عملية أخرى. أعد المحاولة." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to save order");
                return StatusCode(500, new { message = "حدث خطأ أثناء الحفظ. حاول مرة أخرى." });
            }
        }

        [Authorize(Policy = AppPermissions.Dashboard)]
        public IActionResult Profits() => RedirectToAction("Index", "Reports", new { period = "month" });


        [Authorize(Policy = AppPermissions.Products)]
        public async Task<IActionResult> ProductHistory(int id)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            var entries = await _context.StockLedgers
                .Where(l => l.ProductId == id)
                .OrderByDescending(l => l.OccurredAt)
                .ToListAsync();

            var history = entries.Select(l => new ProductMovementViewModel
            {
                Date = l.OccurredAt,
                Type = MovementLabel(l.MovementType),
                Quantity = l.QuantityChange,
                QuantityBefore = l.QuantityBefore,
                QuantityAfter = l.QuantityAfter,
                Reference = l.Reference ?? string.Empty,
                Reason = l.Reason
            }).ToList();

            ViewBag.ProductName = product.Name;
            ViewBag.CurrentStock = product.StockQuantity;

            return View(history);
        }

        private static string MovementLabel(string movementType) => movementType switch
        {
            StockMovementTypes.Sale => "بيع (صادر)",
            StockMovementTypes.Purchase => "شراء (وارد)",
            StockMovementTypes.PurchaseReturn => "مرتجع توريد (صادر)",
            StockMovementTypes.Return => "مرتجع (وارد)",
            StockMovementTypes.Adjustment => "تعديل يدوي",
            StockMovementTypes.Opening => "رصيد أول المدة",
            _ => movementType
        };

        [Authorize(Policy = AppPermissions.Products)]
        public IActionResult Import() => View();

        [HttpPost]
        [Authorize(Policy = AppPermissions.Products)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "اختر ملف CSV.");
                return View();
            }

            using var reader = new StreamReader(file.OpenReadStream());
            var content = await reader.ReadToEndAsync();
            var existingBarcodes = await _context.Products
                .Where(p => p.Barcode != null && p.Barcode != "")
                .Select(p => p.Barcode!)
                .ToListAsync();
            var existingNames = await _context.Products
                .Select(p => p.Name)
                .ToListAsync();
            var products = ProductCsvImporter.Parse(content, existingBarcodes, existingNames, out var skippedDuplicates, out var skippedInvalid);
            if (products.Count == 0)
            {
                TempData["Error"] = skippedDuplicates > 0 || skippedInvalid > 0
                    ? "كل الصفوف مكررة أو بياناتها غير صالحة."
                    : "لا توجد صفوف صالحة في الملف.";
                return RedirectToAction(nameof(Import));
            }

            foreach (var product in products)
            {
                _context.Products.Add(product);
            }

            await _context.SaveChangesAsync();
            foreach (var product in products.Where(p => p.TrackInventory && p.StockQuantity != 0))
            {
                _stockLedger.RecordSnapshot(product.Id, 0, product.StockQuantity, StockMovementTypes.Opening, "استيراد CSV", $"منتج #{product.Id}");
            }

            await _context.SaveChangesAsync();
            var extras = new List<string>();
            if (skippedDuplicates > 0)
            {
                extras.Add($"تخطي {skippedDuplicates} باركود مكرر");
            }

            if (skippedInvalid > 0)
            {
                extras.Add($"تخطي {skippedInvalid} صف بسعر أو كمية غير صالحة");
            }

            TempData["Success"] = $"تم استيراد {products.Count} صنف" + (extras.Count > 0 ? " و" + string.Join(" و", extras) + "." : ".");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Policy = AppPermissions.Products)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdjustStock([FromBody] AdjustStockRequest request)
        {
            var result = await TryAdjustStockAsync(_context, _stockLedger, User.Identity?.Name, request);
            return result.Ok ? Ok(new { message = result.Message }) : BadRequest(new { message = result.Message });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HoldSale([FromBody] HoldSaleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PayloadJson) || request.LineCount <= 0 || request.TotalAmount < 0)
            {
                return BadRequest(new { message = "السلة فارغة أو بياناتها غير صالحة." });
            }

            var userName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName))
            {
                return Unauthorized();
            }

            var held = new HeldSale
            {
                UserName = userName,
                PayloadJson = request.PayloadJson,
                LineCount = request.LineCount,
                TotalAmount = request.TotalAmount,
                CreatedAt = DateTime.Now
            };
            _context.HeldSales.Add(held);
            await _context.SaveChangesAsync();
            return Ok(new { message = "تم تعليق الفاتورة.", id = held.Id });
        }

        [HttpGet]
        public async Task<IActionResult> HeldSales()
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName))
            {
                return Unauthorized();
            }

            var rows = await _context.HeldSales
                .Where(h => h.UserName == userName)
                .OrderByDescending(h => h.CreatedAt)
                .Select(h => new { h.Id, h.CreatedAt, h.LineCount, h.TotalAmount })
                .ToListAsync();
            return Json(rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreHeldSale(int id)
        {
            var userName = User.Identity?.Name;
            var held = await _context.HeldSales.FirstOrDefaultAsync(h => h.Id == id && h.UserName == userName);
            if (held == null)
            {
                return NotFound(new { message = "الفاتورة المعلقة غير موجودة." });
            }

            var payload = held.PayloadJson;
            _context.HeldSales.Remove(held);
            await _context.SaveChangesAsync();
            return Json(new { payloadJson = payload });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteHeldSale(int id)
        {
            var userName = User.Identity?.Name;
            var held = await _context.HeldSales.FirstOrDefaultAsync(h => h.Id == id && h.UserName == userName);
            if (held == null)
            {
                return NotFound();
            }

            _context.HeldSales.Remove(held);
            await _context.SaveChangesAsync();
            return Ok(new { message = "تم حذف الفاتورة المعلقة." });
        }

        private async Task LoadCategoriesAsync()
        {
            ViewBag.Categories = await _context.Products
                .Where(p => p.Category != null && p.Category != "")
                .Select(p => p.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }

        internal static async Task<(bool Ok, string Message)> TryAdjustStockAsync(
            ApplicationDbContext db,
            StockLedgerService ledger,
            string? userName,
            AdjustStockRequest request)
        {
            if (request.NewQuantity < 0)
            {
                return (false, InputRules.QuantityCannotBeNegative);
            }

            if (!StockAdjustmentReasons.IsKnown(request.Reason))
            {
                return (false, "سبب التسوية غير معروف.");
            }

            if (await db.GetOpenShiftIdAsync(userName) is null)
            {
                return (false, "يجب فتح وردية أولاً قبل تسوية المخزن.");
            }

            var product = await db.GetProductWithRowLockAsync(request.ProductId);
            if (product == null)
            {
                return (false, "المنتج غير موجود.");
            }

            if (!product.TrackInventory)
            {
                return (false, "هذا الصنف غير متتبَّع في المخزن.");
            }

            var delta = request.NewQuantity - product.StockQuantity;
            if (delta == 0)
            {
                return (false, "الكمية لم تتغير.");
            }

            var label = StockAdjustmentReasons.Label(request.Reason);
            var notes = string.IsNullOrWhiteSpace(request.Notes) ? label : $"{label} — {request.Notes.Trim()}";
            ledger.ApplyDelta(product, delta, StockMovementTypes.Adjustment, notes, $"تسوية #{product.Id}");
            await db.SaveChangesAsync();
            return (true, "تم تحديث كمية المخزن.");
        }
    }
}
