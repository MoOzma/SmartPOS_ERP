using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Controllers
{
    public class PurchasesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PurchasesController(ApplicationDbContext context)
        {
            _context = context;
        }


        public async Task<IActionResult> Index()
        {
            // أضف Include(i => i.Details) لضمان تحميل الأصناف وحساب الإجمالي
            var invoices = await _context.PurchaseInvoices
                .Include(i => i.Supplier)
                .Include(i => i.Details)
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();

            return View(invoices);
        }

        // 2. عرض تفاصيل فاتورة واحدة
        public async Task<IActionResult> Details(int id)
        {
            var invoice = await _context.PurchaseInvoices
    .Include(i => i.Supplier)
    .Include(i => i.Details) // جلب تفاصيل الأصناف
        .ThenInclude(d => d.Product) // تأكد أن الموديل PurchaseDetail يحتوي على خاصية Product
    .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null) return NotFound();

            return View(invoice);
        }


        public async Task<IActionResult> Create()
        {
            ViewBag.Suppliers = await _context.Suppliers.ToListAsync();
            ViewBag.Products = await _context.Products.ToListAsync();
            return View();
        }

        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> SavePurchase([FromBody] PurchaseViewModel model)
        {
            if (model == null || !model.Items.Any()) return BadRequest("بيانات الفاتورة فارغة");

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var invoice = new PurchaseInvoice
                    {
                        SupplierId = model.SupplierId,
                        // تأكد من استخدام الاسم الصحيح: PurchaseDate
                        InvoiceDate = model.PurchaseDate == default ? DateTime.Now : model.PurchaseDate,
                        Details = new List<PurchaseDetail>()
                    };

                    foreach (var item in model.Items)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            decimal totalNewUnits = item.PackageQuantity * item.UnitsPerPackage;
                            product.StockQuantity += totalNewUnits;
                            product.CostPrice = item.PackageCost / item.UnitsPerPackage;
                        }

                        invoice.Details.Add(new PurchaseDetail
                        {
                            ProductId = item.ProductId,
                            PackageQuantity = item.PackageQuantity,
                            UnitsPerPackage = item.UnitsPerPackage,
                            TotalUnits = item.PackageQuantity * item.UnitsPerPackage,
                            PackageCost = item.PackageCost,
                            UnitCost = item.PackageCost / item.UnitsPerPackage
                        });
                    }

                    _context.PurchaseInvoices.Add(invoice);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new { message = "تم تسجيل المشتريات وتحديث المخزون بنجاح" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, "خطأ أثناء الحفظ: " + ex.Message);
                }
            }
        }


        [HttpPost]
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
                    .ThenInclude(i => i.Details) // لجلب المشتريات وحساب إجمالي الـ 50 ألف
                .Include(s => s.Payments) // لجلب سجل المدفوعات (الـ 5 آلاف)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supplier == null) return NotFound();

            return View(supplier);
        }

        // 2. أكشن لتسجيل عملية دفع مالي للمورد (صرف مبلغ)
        [HttpPost]
        public async Task<IActionResult> PaySupplier(int supplierId, decimal amount, DateTime paymentDate, string? notes)
        {
            if (amount <= 0) return BadRequest("المبلغ يجب أن يكون أكبر من صفر");

            var payment = new SupplierPayment
            {
                SupplierId = supplierId,
                AmountPaid = amount,
                // نستخدم التاريخ المرسل من النموذج بدلاً من تاريخ اللحظة
                PaymentDate = paymentDate,
                Notes = notes ?? "بدون ملاحظات"
            };

            _context.SupplierPayments.Add(payment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(SupplierAccount), new { id = supplierId });
        }

        public async Task<IActionResult> Suppliers()
        {
            // جلب الموردين مع فواتيرهم ومدفوعاتهم لحساب الرصيد المتبقي لكل واحد
            var suppliers = await _context.Suppliers
                .Include(s => s.Invoices)
                    .ThenInclude(i => i.Details)
                .Include(s => s.Payments)
                .ToListAsync();

            return View(suppliers);
        }

    }
}