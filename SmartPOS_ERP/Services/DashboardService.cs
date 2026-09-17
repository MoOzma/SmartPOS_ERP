using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Services;

public class DashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DashboardService> _logger;
    private readonly ShiftCashService _shiftCash;

    public DashboardService(ApplicationDbContext db, ILogger<DashboardService> logger, ShiftCashService shiftCash)
    {
        _db = db;
        _logger = logger;
        _shiftCash = shiftCash;
    }

    public async Task<DashboardViewModel> BuildAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var model = new DashboardViewModel();
        var dayStart = now.Date;
        var dayEnd = dayStart.AddDays(1);
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var prevMonthStart = monthStart.AddMonths(-1);
        var hoursFrom = now.AddDays(-30);
        var calendarStart = new DateTime(now.Year, 1, 1);
        var calendarEnd = calendarStart.AddYears(1);
        model.MonthlySales = Enumerable.Range(0, 12)
            .Select(i => calendarStart.AddMonths(i))
            .Select(month => new DashboardMonthPoint { Label = month.ToString("yyyy-MM") })
            .ToList();
        model.HourlySales = Enumerable.Range(0, 24)
            .Select(hour => new DashboardHourPoint { Hour = hour })
            .ToList();

        try
        {
            model.TodaySales = await _db.Orders
                .Where(o => o.OrderDate >= dayStart && o.OrderDate < dayEnd)
                .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0;
            model.MonthSales = await _db.Orders
                .Where(o => o.OrderDate >= monthStart && o.OrderDate < monthEnd)
                .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0;
            model.MonthInvoiceCount = await _db.Orders
                .CountAsync(o => o.OrderDate >= monthStart && o.OrderDate < monthEnd, cancellationToken);
            model.PrevMonthInvoiceCount = await _db.Orders
                .CountAsync(o => o.OrderDate >= prevMonthStart && o.OrderDate < monthStart, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard sales cards failed");
        }

        try
        {
            var gross = await _db.OrderDetails
                .Where(d => d.Order != null && d.Order.OrderDate >= monthStart && d.Order.OrderDate < monthEnd)
                .SumAsync(d => (decimal?)((d.UnitPrice - d.UnitCost) * d.Quantity), cancellationToken) ?? 0;
            var discounts = await _db.Orders
                .Where(o => o.OrderDate >= monthStart && o.OrderDate < monthEnd)
                .SumAsync(o => (decimal?)o.DiscountAmount, cancellationToken) ?? 0;
            model.MonthExpenses = await _db.Expenses
                .Where(e => e.ExpenseDate >= monthStart && e.ExpenseDate < monthEnd)
                .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0;
            model.MonthProfit = gross - discounts - model.MonthExpenses;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard profit failed");
        }

        try
        {
            var purchases = await _db.PurchaseDetails
                .Where(d => d.PurchaseInvoice != null && !d.PurchaseInvoice.IsVoided)
                .SumAsync(d => (decimal?)(d.PackageQuantity * d.PackageCost), cancellationToken) ?? 0;
            var paid = await _db.SupplierPayments
                .SumAsync(p => (decimal?)p.AmountPaid, cancellationToken) ?? 0;
            var returned = await _db.PurchaseReturnDetails
                .SumAsync(d => (decimal?)d.LineTotal, cancellationToken) ?? 0;
            model.SupplierPayables = purchases - paid - returned;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard payables failed");
        }

        try
        {
            model.StockValue = await _db.Products
                .Where(p => p.TrackInventory)
                .SumAsync(p => (decimal?)(p.StockQuantity * p.CostPrice), cancellationToken) ?? 0;
            model.MonthPurchaseCount = await _db.PurchaseInvoices
                .CountAsync(p => !p.IsVoided && p.InvoiceDate >= monthStart && p.InvoiceDate < monthEnd, cancellationToken);
            model.PrevMonthPurchaseCount = await _db.PurchaseInvoices
                .CountAsync(p => !p.IsVoided && p.InvoiceDate >= prevMonthStart && p.InvoiceDate < monthStart, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard extra stock and purchase cards failed");
        }

        try
        {
            var openShifts = await _db.Shifts
                .CountAsync(s => s.ClosedAt == null, cancellationToken);
            model.OpenShiftCount = openShifts;
            if (openShifts > 0)
            {
                model.CashBalance = await _shiftCash.ExpectedForOpenShiftsAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard cash balance failed");
        }

        try
        {
            model.MonthReturnCount = await _db.SalesReturns
                .CountAsync(r => r.ReturnDate >= monthStart && r.ReturnDate < monthEnd, cancellationToken);
            model.MonthReturnAmount = await _db.SalesReturns
                .Where(r => r.ReturnDate >= monthStart && r.ReturnDate < monthEnd)
                .SumAsync(r => (decimal?)r.RefundAmount, cancellationToken) ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard returns failed");
        }

        try
        {
            var months = await _db.Orders
                .Where(o => o.OrderDate >= calendarStart && o.OrderDate < calendarEnd)
                .Select(o => new { o.OrderDate, o.TotalAmount })
                .ToListAsync(cancellationToken);
            var monthExpenses = await _db.Expenses
                .Where(e => e.ExpenseDate >= calendarStart && e.ExpenseDate < calendarEnd)
                .Select(e => new { e.ExpenseDate, e.Amount })
                .ToListAsync(cancellationToken);
            var monthPurchases = await _db.PurchaseDetails
                .Where(d => d.PurchaseInvoice != null
                    && d.PurchaseInvoice.InvoiceDate >= calendarStart
                    && d.PurchaseInvoice.InvoiceDate < calendarEnd)
                .Select(d => new { d.PurchaseInvoice!.InvoiceDate, Amount = d.PackageQuantity * d.PackageCost })
                .ToListAsync(cancellationToken);

            model.MonthlySales = Enumerable.Range(0, 12)
                .Select(i => calendarStart.AddMonths(i))
                .Select(month =>
                {
                    var bucket = months.Where(o => o.OrderDate.Year == month.Year && o.OrderDate.Month == month.Month).ToList();
                    return new DashboardMonthPoint
                    {
                        Label = month.ToString("yyyy-MM"),
                        Total = bucket.Sum(x => x.TotalAmount),
                        Count = bucket.Count,
                        Expenses = monthExpenses
                            .Where(e => e.ExpenseDate.Year == month.Year && e.ExpenseDate.Month == month.Month)
                            .Sum(e => e.Amount),
                        Purchases = monthPurchases
                            .Where(p => p.InvoiceDate.Year == month.Year && p.InvoiceDate.Month == month.Month)
                            .Sum(p => p.Amount)
                    };
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard monthly chart failed");
        }

        try
        {
            model.TopProducts = await _db.OrderDetails
                .Where(d => d.Product != null)
                .GroupBy(d => new { d.ProductId, Name = d.Product!.Name })
                .Select(g => new DashboardNamedValue
                {
                    Name = g.Key.Name,
                    Quantity = g.Sum(x => x.Quantity),
                    Sales = g.Sum(x => x.Quantity * x.UnitPrice)
                })
                .OrderByDescending(x => x.Quantity)
                .Take(5)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard top products failed");
        }

        try
        {
            var recent = await _db.Orders
                .Where(o => o.OrderDate >= hoursFrom)
                .Select(o => new { o.OrderDate, o.TotalAmount })
                .ToListAsync(cancellationToken);

            model.HourlySales = Enumerable.Range(0, 24)
                .Select(hour =>
                {
                    var bucket = recent.Where(o => o.OrderDate.Hour == hour).ToList();
                    return new DashboardHourPoint
                    {
                        Hour = hour,
                        Total = bucket.Sum(x => x.TotalAmount),
                        Count = bucket.Count
                    };
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard hourly chart failed");
        }

        try
        {
            model.RecentInvoices = await _db.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(8)
                .Select(o => new DashboardInvoiceRow
                {
                    Id = o.Id,
                    Date = o.OrderDate,
                    Total = o.TotalAmount
                })
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard recent invoices failed");
        }

        try
        {
            model.LowStock = await _db.Products
                .Where(p => p.TrackInventory && (p.StockQuantity <= p.ReorderLevel || p.StockQuantity <= 0))
                .OrderBy(p => p.StockQuantity)
                .Select(p => new DashboardLowStockRow
                {
                    Id = p.Id,
                    Name = p.Name,
                    Stock = p.StockQuantity,
                    ReorderLevel = p.ReorderLevel,
                    Unit = p.Unit
                })
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard low stock failed");
        }

        try
        {
            model.CustomerReceivables = await _db.CreditInvoices
                .Where(i => i.Status == CreditInvoiceStatuses.Open)
                .Select(i => i.TotalAmount - i.Payments.Sum(p => p.Amount))
                .SumAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard receivables failed");
        }

        try
        {
            var watchUntil = ProductExpiry.ExclusiveEnd(now);
            model.ExpiringCount = await _db.Products
                .CountAsync(p => p.TrackInventory && p.ExpiryDate != null && p.ExpiryDate < watchUntil, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard expiry failed");
        }

        return model;
    }
}
