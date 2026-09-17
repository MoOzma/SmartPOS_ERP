using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Services;

public class SalesProfitReportService
{
    private static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar-EG");
    private readonly ApplicationDbContext _db;

    public SalesProfitReportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SalesProfitReportViewModel> BuildAsync(
        string? period,
        DateTime? selectedDate,
        int? selectedMonth,
        int? selectedYear,
        DateTime now,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var kind = period?.Trim().ToLowerInvariant() switch
        {
            "month" => "month",
            "range" => "range",
            _ => "day"
        };
        var date = (selectedDate ?? now).Date;
        var year = selectedYear is >= 2000 and <= 2100 ? selectedYear.Value : now.Year;
        var month = selectedMonth is >= 1 and <= 12 ? selectedMonth.Value : now.Month;
        var rangeStart = (fromDate ?? now.AddDays(-7)).Date;
        var rangeEnd = (toDate ?? now).Date;
        if (rangeEnd < rangeStart)
        {
            (rangeStart, rangeEnd) = (rangeEnd, rangeStart);
        }

        DateTime start;
        DateTime end;
        if (kind == "month")
        {
            start = new DateTime(year, month, 1);
            end = start.AddMonths(1);
            date = start;
            rangeStart = start;
            rangeEnd = end.AddDays(-1);
        }
        else if (kind == "range")
        {
            start = rangeStart;
            end = rangeEnd.AddDays(1);
            date = start;
            year = start.Year;
            month = start.Month;
        }
        else
        {
            start = date;
            end = start.AddDays(1);
            year = start.Year;
            month = start.Month;
            rangeStart = start;
            rangeEnd = start;
        }

        var orders = await _db.Orders
            .Where(o => o.OrderDate >= start && o.OrderDate < end)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new DashboardInvoiceRow
            {
                Id = o.Id,
                Date = o.OrderDate,
                Total = o.TotalAmount
            })
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(o => o.Id).ToList();
        var details = orderIds.Count == 0
            ? []
            : await _db.OrderDetails
                .Where(d => orderIds.Contains(d.OrderId))
                .Select(d => new { d.OrderId, d.ProductId, d.Quantity, d.UnitPrice, d.UnitCost })
                .ToListAsync(cancellationToken);

        var returns = await _db.SalesReturns
            .Where(r => r.ReturnDate >= start && r.ReturnDate < end)
            .Select(r => new { r.OrderId, r.ProductId, r.Quantity, r.RefundAmount })
            .ToListAsync(cancellationToken);

        var allOrderReturns = orderIds.Count == 0
            ? []
            : await _db.SalesReturns
                .Where(r => orderIds.Contains(r.OrderId))
                .Select(r => new { r.OrderId, r.ProductId, r.Quantity, r.RefundAmount })
                .ToListAsync(cancellationToken);

        var returnOrderIds = returns.Select(r => r.OrderId).Distinct().ToList();
        var returnCosts = returnOrderIds.Count == 0
            ? []
            : await _db.OrderDetails
                .Where(d => returnOrderIds.Contains(d.OrderId))
                .Select(d => new { d.OrderId, d.ProductId, d.UnitCost })
                .ToListAsync(cancellationToken);

        var expenses = await _db.Expenses
            .Where(e => e.ExpenseDate >= start && e.ExpenseDate < end)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0;

        decimal ReturnProfitHit(int orderId, int productId, decimal quantity, decimal refundAmount)
        {
            var unitCost = details.FirstOrDefault(d => d.OrderId == orderId && d.ProductId == productId)?.UnitCost
                ?? returnCosts.FirstOrDefault(d => d.OrderId == orderId && d.ProductId == productId)?.UnitCost
                ?? 0m;
            return refundAmount - unitCost * quantity;
        }

        var remainingLineProfit = details.Sum(d => (d.UnitPrice - d.UnitCost) * d.Quantity);
        var periodDiscounts = await _db.Orders
            .Where(o => o.OrderDate >= start && o.OrderDate < end)
            .SumAsync(o => (decimal?)o.DiscountAmount, cancellationToken) ?? 0;
        var originalProfitHits = allOrderReturns.Sum(r => ReturnProfitHit(r.OrderId, r.ProductId, r.Quantity, r.RefundAmount));
        var periodProfitHits = returns.Sum(r => ReturnProfitHit(r.OrderId, r.ProductId, r.Quantity, r.RefundAmount));
        var originalSales = orders.Sum(o => o.Total) + allOrderReturns.Sum(r => r.RefundAmount);
        var returnAmount = returns.Sum(r => r.RefundAmount);
        var grossSales = originalSales;
        var grossProfit = remainingLineProfit + originalProfitHits - periodProfitHits - periodDiscounts;
        var netSales = grossSales - returnAmount;

        return new SalesProfitReportViewModel
        {
            PeriodKind = kind,
            SelectedDate = date,
            SelectedMonth = month,
            SelectedYear = year,
            FromDate = rangeStart,
            ToDate = rangeEnd,
            PeriodLabel = kind switch
            {
                "month" => start.ToString("MMMM yyyy", Arabic),
                "range" => "من " + rangeStart.ToString("d MMMM yyyy", Arabic) + " إلى " + rangeEnd.ToString("d MMMM yyyy", Arabic),
                _ => start.ToString("dddd d MMMM yyyy", Arabic)
            },
            GrossSales = grossSales,
            Returns = returnAmount,
            NetSales = netSales,
            GrossProfit = grossProfit,
            Expenses = expenses,
            NetProfit = grossProfit - expenses,
            InvoiceCount = orders.Count,
            ReturnCount = returns.Count,
            Invoices = orders
        };
    }
}
