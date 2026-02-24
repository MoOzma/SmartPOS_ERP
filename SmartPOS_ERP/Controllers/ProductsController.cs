using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        public IActionResult Create()
        {
            return View();
        }

      
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Barcode,CostPrice,SalePrice,TrackInventory,StockQuantity,Unit,ReorderLevel,ImagePath")] Product product)
        {
            if (ModelState.IsValid)
            {
                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Barcode,CostPrice,SalePrice,TrackInventory,StockQuantity,ReorderLevel,Unit,ImagePath")] Product product)
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
        public async Task<IActionResult> SaveOrder([FromBody] OrderViewModel model)
        {
            if (model == null || !model.OrderDetails.Any()) return BadRequest();

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var order = new Order
                    {
                        OrderDate = DateTime.Now,
                        TotalAmount = model.TotalAmount,
                        TaxAmount = 0,
                        OrderDetails = new List<OrderDetail>()
                    };

                    foreach (var item in model.OrderDetails)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);

                        if (product != null && product.TrackInventory) 
                        {
                            // التحقق من الوزن بالجرامات (Decimal Comparison)
                            if (product.StockQuantity < item.Quantity)
                            {
                                string unitName = product.Unit == "Kilo" ? "كجم" : "قطعة";
                                return BadRequest(new
                                {
                                    message = $"خطأ في كمية {product.Name}: المطلوب ({item.Quantity} {unitName})، والمتاح في المخزن ({product.StockQuantity} {unitName}) فقط!"
                                });
                            }

                            product.StockQuantity -= item.Quantity;
                        }

                        order.OrderDetails.Add(new OrderDetail
                        {
                            ProductId = item.ProductId,
                            Quantity = item.Quantity, 
                            UnitPrice = item.UnitPrice
                        });
                    }

                    _context.Orders.Add(order);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new { message = "تمت العملية بنجاح" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, "حدث خطأ فني: " + ex.Message);
                }
            }
        }

   //   [Authorize(Roles = "Admin")] 
        public async Task<IActionResult> Profits()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(d => d.Product)
                .ToListAsync();

            var allExpenses = await _context.Expenses.ToListAsync();

            var monthlyProfits = orders
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => {
                    var monthKey = $"{g.Key.Year}-{g.Key.Month:D2}";

                    // ربح البضاعة = (سعر البيع - سعر التكلفة) * الكمية
                    var grossProfit = g.Sum(o => o.OrderDetails.Sum(d =>
                        (d.UnitPrice - (d.Product?.CostPrice ?? 0)) * d.Quantity));

                    // مصاريف هذا الشهر
                    var monthlyExpense = allExpenses
                        .Where(e => e.ExpenseDate.Year == g.Key.Year && e.ExpenseDate.Month == g.Key.Month)
                        .Sum(e => e.Amount);

                    return new MonthlyProfitViewModel
                    {
                        Month = monthKey,
                        TotalSales = g.Sum(o => o.TotalAmount),
                        TotalProfit = grossProfit - monthlyExpense
                    };
                })
                .OrderByDescending(g => g.Month)
                .ToList();

            return View(monthlyProfits);
        }


        public async Task<IActionResult> ProductHistory(int id)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            // 1. جلب سجلات الشراء (وارد)
            var purchases = await _context.PurchaseDetails
                .Include(pd => pd.PurchaseInvoice)
                .Where(pd => pd.ProductId == id)
                .Select(pd => new ProductMovementViewModel
                {
                    Date = pd.PurchaseInvoice.InvoiceDate,
                    Type = "شراء (وارد)",
                    Quantity = pd.PackageQuantity, // حسب تصميمك في PurchaseDetail
                    Price = pd.PackageCost,
                    Reference = "فاتورة شراء #" + pd.PurchaseInvoice.Id
                }).ToListAsync();

            // 2. جلب سجلات البيع (صادر)
            var sales = await _context.OrderDetails
                .Include(od => od.Order)
                .Where(od => od.ProductId == id)
                .Select(od => new ProductMovementViewModel
                {
                    Date = od.Order.OrderDate,
                    Type = "بيع (صادر)",
                    Quantity = od.Quantity,
                    Price = od.UnitPrice,
                    Reference = "فاتورة بيع #" + od.Order.Id
                }).ToListAsync();

            // 3. دمج السجلين وترتيبهم بالتاريخ
            var history = purchases.Concat(sales).OrderByDescending(h => h.Date).ToList();

            ViewBag.ProductName = product.Name;
            ViewBag.CurrentStock = product.StockQuantity;

            return View(history);
        }



    }
}
