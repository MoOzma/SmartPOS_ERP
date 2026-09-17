using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Services;

public class ExpenseReportService
{
    private static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar-EG");
    private readonly ApplicationDbContext _db;

    public ExpenseReportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ExpenseReportViewModel> BuildAsync(
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

        var items = await _db.Expenses
            .Where(e => e.ExpenseDate >= start && e.ExpenseDate < end)
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.Id)
            .ToListAsync(cancellationToken);

        var total = items.Sum(e => e.Amount);
        var count = items.Count;
        var categories = items
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Category) ? "بدون فئة" : e.Category)
            .Select(g => new ExpenseCategoryRow
            {
                Name = g.Key,
                Amount = g.Sum(e => e.Amount),
                Count = g.Count(),
                Percent = total == 0 ? 0 : g.Sum(e => e.Amount) / total * 100m
            })
            .OrderByDescending(c => c.Amount)
            .ThenBy(c => c.Name)
            .ToList();

        return new ExpenseReportViewModel
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
            TotalAmount = total,
            Count = count,
            AverageAmount = count == 0 ? 0 : total / count,
            TopCategory = categories.FirstOrDefault()?.Name ?? "",
            Items = items,
            Categories = categories
        };
    }
}
