using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Services;

public class PurchaseInvoiceReportService
{
    private static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar-EG");
    private readonly ApplicationDbContext _db;

    public PurchaseInvoiceReportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PurchaseInvoiceReportViewModel> BuildAsync(
        string? period,
        DateTime? selectedDate,
        int? selectedMonth,
        int? selectedYear,
        DateTime now,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        var kind = period?.Trim().ToLowerInvariant() switch
        {
            "day" => "day",
            "range" => "range",
            _ => "month"
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

        var invoices = await _db.PurchaseInvoices
            .Include(i => i.Supplier)
            .Include(i => i.Details)
            .Where(i => i.InvoiceDate >= start && i.InvoiceDate < end)
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.Id)
            .ToListAsync(cancellationToken);

        var q = query?.Trim();
        if (!string.IsNullOrWhiteSpace(q))
        {
            invoices = invoices.Where(i =>
                    (i.Supplier?.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    i.Id.ToString(CultureInfo.InvariantCulture).Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var items = invoices.Select(i =>
        {
            var details = i.Details ?? [];
            return new PurchaseInvoiceReportRow
            {
                Id = i.Id,
                SupplierId = i.SupplierId,
                SupplierName = i.Supplier?.Name ?? "—",
                InvoiceDate = i.InvoiceDate,
                LineCount = details.Count,
                TotalUnits = details.Sum(d => d.TotalUnits),
                TotalAmount = details.Sum(d => d.PackageQuantity * d.PackageCost),
                IsVoided = i.IsVoided
            };
        }).ToList();

        var active = items.Where(x => !x.IsVoided).ToList();
        var total = active.Sum(x => x.TotalAmount);
        var suppliers = active
            .GroupBy(x => new { x.SupplierId, x.SupplierName })
            .Select(g => new PurchaseSupplierRow
            {
                SupplierId = g.Key.SupplierId,
                Name = g.Key.SupplierName,
                InvoiceCount = g.Count(),
                Amount = g.Sum(x => x.TotalAmount),
                Percent = total == 0 ? 0 : g.Sum(x => x.TotalAmount) / total * 100m
            })
            .OrderByDescending(s => s.Amount)
            .ThenBy(s => s.Name)
            .ToList();

        return new PurchaseInvoiceReportViewModel
        {
            PeriodKind = kind,
            SelectedDate = date,
            SelectedMonth = month,
            SelectedYear = year,
            FromDate = rangeStart,
            ToDate = rangeEnd,
            Query = q,
            PeriodLabel = kind switch
            {
                "month" => start.ToString("MMMM yyyy", Arabic),
                "range" => "من " + rangeStart.ToString("d MMMM yyyy", Arabic) + " إلى " + rangeEnd.ToString("d MMMM yyyy", Arabic),
                _ => start.ToString("dddd d MMMM yyyy", Arabic)
            },
            TotalAmount = total,
            TotalUnits = active.Sum(x => x.TotalUnits),
            InvoiceCount = active.Count,
            SupplierCount = suppliers.Count,
            TopSupplier = suppliers.FirstOrDefault()?.Name ?? "",
            Items = items,
            Suppliers = suppliers
        };
    }
}
