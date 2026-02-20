using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Products
        public IActionResult Index()
        {
            var products = _context.Products.ToList(); // تأكد من جلب البيانات
            return View(products); // تأكد من تمرير القائمة للموديل
        }

        // GET: Products/Details/5
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

        // GET: Products/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Products/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Barcode,CostPrice,SalePrice,TaxRate,TrackInventory,StockQuantity,ReorderLevel,ImagePath")] Product product)
        {
            if (ModelState.IsValid)
            {
                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // GET: Products/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {

            // التأكد من أن المستخدم مدير قبل عرض التقارير
            if (HttpContext.Session.GetString("UserRole") != "Admin")
            {
                return RedirectToAction("Login", "Account"); // توجيهه لصفحة الدخول
            }

            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        // POST: Products/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Barcode,CostPrice,SalePrice,TaxRate,TrackInventory,StockQuantity,ReorderLevel,ImagePath")] Product product)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(product);
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
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // GET: Products/Delete/5
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

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
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



        [HttpPost]
        public async Task<IActionResult> SaveOrder([FromBody] OrderViewModel orderData)
        {
            if (orderData == null || orderData.OrderDetails == null)
                return BadRequest("بيانات الفاتورة غير مكتملة");

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. تسجيل الفاتورة الرئيسية (اختياري حسب جدولك)
                    // ... كود حفظ الفاتورة ...

                    foreach (var item in orderData.OrderDetails)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);

                        if (product != null)
                        {
                            // التحقق من توفر الكمية (سواء كانت جرامات أو قطع)
                            if (product.StockQuantity < item.Quantity)
                            {
                                return BadRequest($"الكمية المطلوبة من {product.Name} غير متوفرة");
                            }

                            // خصم الكمية (المتصفح يرسل الكمية محسوبة جاهزة سواء كانت وزن أو قطعة)
                            product.StockQuantity -= item.Quantity;
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return Ok();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, "حدث خطأ أثناء معالجة العملية");
                }
            }
        }


        // GET: Products/Edit/5




        public async Task<IActionResult> Reports()
        {
            // تأمين الصفحة للمدير فقط
            if (HttpContext.Session.GetString("UserRole") != "Admin")
                return RedirectToAction("Login", "Account");

            // 1. حساب إجمالي المبيعات (من جدول الطلبات الذي أنشأناه سابقاً)
            var totalSales = await _context.OrderDetails.SumAsync(od => od.Quantity * od.UnitPrice);

            // 2. حساب صافي الأرباح 
            // التفكير البرمجي: الربح = (سعر البيع - سعر التكلفة) * الكمية
            var products = await _context.Products.ToListAsync();
            var totalProfit = await _context.OrderDetails
                .Include(od => od.Product)
                .SumAsync(od => od.Quantity * (od.UnitPrice - od.Product.CostPrice));

            // 3. عدد المنتجات التي مخزونها أقل من 5
            var lowStockCount = products.Count(p => p.StockQuantity < 5);

            // نرسل البيانات للواجهة عبر ViewBag
            ViewBag.TotalSales = totalSales;
            ViewBag.TotalProfit = totalProfit;
            ViewBag.LowStockCount = lowStockCount;
            ViewBag.OrdersCount = await _context.OrderDetails.Select(od => od.Id).CountAsync();

            // التفكير البرمجي: نريد مبيعات آخر 7 أيام
            var salesLast7Days = await _context.OrderDetails
                .GroupBy(od => od.Id) // (ملاحظة: هنا يفضل التجميع حسب تاريخ الفاتورة إذا كان موجوداً)
                .Select(g => new { Day = "اليوم", Total = g.Sum(x => x.Quantity * x.UnitPrice) })
                .Take(7)
                .ToListAsync();

            // نرسل الأسماء والقيم كقوائم منفصلة للرسم البياني
            ViewBag.ChartLabels = Newtonsoft.Json.JsonConvert.SerializeObject(new[] { "السبت", "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة" });
            ViewBag.ChartData = Newtonsoft.Json.JsonConvert.SerializeObject(new[] { 120, 150, 180, 100, 250, 300, 200 }); // أرقام تجريبية


            return View();
        }





    }
}
