using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SmartPOS_ERP.Controllers
{
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? selectedDate, int? selectedMonth, int? selectedYear)
        {
            var now = DateTime.Now;

            // 1. منطق البحث عن يوم معين
            var targetDate = selectedDate ?? now.Date;
            var dailyOrders = await _context.Orders
                .Where(o => o.OrderDate.Date == targetDate.Date)
                .ToListAsync();

            // 2. منطق البحث عن شهر معين
            var targetMonth = selectedMonth ?? now.Month;
            var targetYear = selectedYear ?? now.Year;
            var monthlyOrders = await _context.Orders
                .Where(o => o.OrderDate.Month == targetMonth && o.OrderDate.Year == targetYear)
                .ToListAsync();

            // إرسال القيم المختارة للفيو للحفاظ عليها في صناديق البحث
            ViewBag.SelectedDate = targetDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedMonth = targetMonth;
            ViewBag.SelectedYear = targetYear;

            // الإحصائيات
            ViewBag.DailyTotal = dailyOrders.Sum(o => o.TotalAmount);
            ViewBag.MonthlyTotal = monthlyOrders.Sum(o => o.TotalAmount);

            return View();
        }


        public async Task<IActionResult> DailyReport()
        {
            var now = DateTime.Now;
            var twelveMonthsAgo = now.AddMonths(-11); // بداية الـ 12 شهر

            // 1. إحصائيات سريعة (اليوم والشهر الحالي)
            var dailyTotal = await _context.Orders
                .Where(o => o.OrderDate.Date == now.Date)
                .SumAsync(o => o.TotalAmount);

            ViewBag.DailyTotal = dailyTotal;

            // 2. جلب مبيعات آخر 12 شهر وتجميعها
            var monthlySalesData = await _context.Orders
                .Where(o => o.OrderDate >= new DateTime(twelveMonthsAgo.Year, twelveMonthsAgo.Month, 1))
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => new {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Total = g.Sum(x => x.TotalAmount),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
                .ToListAsync();

            ViewBag.MonthlySalesData = monthlySalesData;

            return View();
        }


        public async Task<IActionResult> TopFive()
        {
            var now = DateTime.Now;

            // 1. مبيعات اليوم
            var dailyOrders = await _context.Orders
                .Where(o => o.OrderDate.Date == now.Date)
                .ToListAsync();
            ViewBag.DailyTotal = dailyOrders.Sum(o => o.TotalAmount);
            ViewBag.DailyCount = dailyOrders.Count;

            // 2. مبيعات الشهر الحالي
            var monthlyOrders = await _context.Orders
                .Where(o => o.OrderDate.Month == now.Month && o.OrderDate.Year == now.Year)
                .ToListAsync();
            ViewBag.MonthlyTotal = monthlyOrders.Sum(o => o.TotalAmount);
            ViewBag.MonthlyCount = monthlyOrders.Count;

            // 3. أفضل 5 منتجات مبيعاً (إحصائياً)
            var topProducts = await _context.OrderDetails
                .Include(d => d.Product)
                .GroupBy(d => new { d.ProductId, d.Product.Name, d.Product.Unit })
                .Select(g => new {
                    ProductName = g.Key.Name,
                    Unit = g.Key.Unit,
                    TotalQty = g.Sum(x => x.Quantity),
                    TotalSales = g.Sum(x => x.Quantity * x.UnitPrice)
                })
                .OrderByDescending(x => x.TotalQty)
                .Take(5)
                .ToListAsync();

            ViewBag.TopProducts = topProducts;

            return View();
        }


        // تقرير المنتجات التي وصلت لحد إعادة الطلب أو نفدت
        public async Task<IActionResult> LowStockReport()
        {
            // جلب المنتجات التي يقل مخزونها عن حد إعادة الطلب 
                        // أو التي نفدت تماماً (صفر أو أقل)
            var lowStockProducts = await _context.Products
                .Where(p => p.TrackInventory && (p.StockQuantity <= p.ReorderLevel || p.StockQuantity <= 0))
                .OrderBy(p => p.StockQuantity)
                .ToListAsync();

            return View(lowStockProducts);
        }




    }
}