using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Security;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Controllers
{
    [Authorize(Policy = AppPermissions.Dashboard)]
    public class ReportsController : Controller
    {
        private readonly SalesProfitReportService _reports;
        private readonly ApplicationDbContext _db;

        public ReportsController(SalesProfitReportService reports, ApplicationDbContext db)
        {
            _reports = reports;
            _db = db;
        }

        public async Task<IActionResult> Index(
            string? period,
            DateTime? selectedDate,
            int? selectedMonth,
            int? selectedYear,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            var model = await _reports.BuildAsync(
                period,
                selectedDate,
                selectedMonth,
                selectedYear,
                DateTime.Now,
                fromDate,
                toDate,
                cancellationToken);
            return View(model);
        }

        public IActionResult DailyReport()
            => RedirectToAction(nameof(Index), new { period = "day" });

        public IActionResult TopFive() => RedirectToAction("Index", "Dashboard");

        public async Task<IActionResult> LowStockReport(CancellationToken cancellationToken = default)
        {
            var items = await _db.Products
                .AsNoTracking()
                .Where(p => p.TrackInventory && (p.StockQuantity <= p.ReorderLevel || p.StockQuantity <= 0))
                .OrderBy(p => p.StockQuantity)
                .ThenBy(p => p.Name)
                .ToListAsync(cancellationToken);
            return View(items);
        }

        public async Task<IActionResult> ExpiryReport(CancellationToken cancellationToken = default)
        {
            var watchUntil = ProductExpiry.ExclusiveEnd(DateTime.Today);
            var items = await _db.Products
                .AsNoTracking()
                .Where(p => p.TrackInventory && p.ExpiryDate != null && p.ExpiryDate < watchUntil)
                .OrderBy(p => p.ExpiryDate)
                .ThenBy(p => p.Name)
                .ToListAsync(cancellationToken);
            return View(items);
        }
    }
}
