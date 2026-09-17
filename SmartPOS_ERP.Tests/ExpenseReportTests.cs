using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SmartPOS_ERP.Controllers;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Tests;

public class ExpenseReportTests
{
    [Fact]
    public async Task Day_report_includes_only_that_days_expenses_and_category_totals()
    {
        await using var harness = new PosHarness();
        SeedExpense(harness, "إيجار", "إيجار سبتمبر", 100m, new DateTime(2026, 9, 14));
        SeedExpense(harness, "كهرباء/مياه", "فاتورة كهرباء", 40m, new DateTime(2026, 9, 14));
        SeedExpense(harness, "نثريات", "يوم تاني", 20m, new DateTime(2026, 9, 15));

        var model = await new ExpenseReportService(harness.Db)
            .BuildAsync("day", new DateTime(2026, 9, 14), null, null, new DateTime(2026, 9, 14));

        Assert.Equal("day", model.PeriodKind);
        Assert.Equal(140m, model.TotalAmount);
        Assert.Equal(2, model.Count);
        Assert.Equal(70m, model.AverageAmount);
        Assert.Equal("إيجار", model.TopCategory);
        Assert.Equal(2, model.Items.Count);
        Assert.Equal(2, model.Categories.Count);
        Assert.Equal("إيجار", model.Categories[0].Name);
        Assert.Equal(100m, model.Categories[0].Amount);
        Assert.Equal(1, model.Categories[0].Count);
        Assert.Equal(100m / 140m * 100m, model.Categories[0].Percent);
        Assert.DoesNotContain(model.Items, e => e.Description == "يوم تاني");
    }

    [Fact]
    public async Task Month_report_includes_the_whole_month()
    {
        await using var harness = new PosHarness();
        SeedExpense(harness, "إيجار", "سبتمبر", 80m, new DateTime(2026, 9, 2));
        SeedExpense(harness, "صيانة", "سبتمبر 2", 20m, new DateTime(2026, 9, 28));
        SeedExpense(harness, "إيجار", "أغسطس", 50m, new DateTime(2026, 8, 31));

        var model = await new ExpenseReportService(harness.Db)
            .BuildAsync("month", null, 9, 2026, new DateTime(2026, 9, 16));

        Assert.Equal("month", model.PeriodKind);
        Assert.Equal(100m, model.TotalAmount);
        Assert.Equal(2, model.Count);
        Assert.Equal(50m, model.AverageAmount);
        Assert.DoesNotContain(model.Items, e => e.Description == "أغسطس");
    }

    [Fact]
    public async Task Range_report_includes_from_to_dates()
    {
        await using var harness = new PosHarness();
        SeedExpense(harness, "نثريات", "قبل", 10m, new DateTime(2026, 9, 13));
        SeedExpense(harness, "نثريات", "داخل", 30m, new DateTime(2026, 9, 14));
        SeedExpense(harness, "نثريات", "آخر يوم", 5m, new DateTime(2026, 9, 16));
        SeedExpense(harness, "نثريات", "بعد", 8m, new DateTime(2026, 9, 17));

        var model = await new ExpenseReportService(harness.Db)
            .BuildAsync("range", null, null, null, new DateTime(2026, 9, 16),
                new DateTime(2026, 9, 14), new DateTime(2026, 9, 16));

        Assert.Equal("range", model.PeriodKind);
        Assert.Equal(35m, model.TotalAmount);
        Assert.Equal(2, model.Count);
        Assert.Equal(new DateTime(2026, 9, 14), model.FromDate);
        Assert.Equal(new DateTime(2026, 9, 16), model.ToDate);
        Assert.DoesNotContain(model.Items, e => e.Description is "قبل" or "بعد");
    }

    [Fact]
    public async Task Index_returns_the_selected_period_report()
    {
        await using var harness = new PosHarness();
        var controller = new ExpensesController(
            harness.Db,
            NullLogger<ExpensesController>.Instance,
            new ExpenseReportService(harness.Db));
        var result = await controller.Index("month", null, 9, 2026);
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ExpenseReportViewModel>(view.Model);
        Assert.Equal("month", model.PeriodKind);
        Assert.Equal(9, model.SelectedMonth);
        Assert.Equal(2026, model.SelectedYear);
    }

    private static void SeedExpense(PosHarness harness, string category, string description, decimal amount, DateTime date)
    {
        harness.Db.Expenses.Add(new Expense
        {
            Category = category,
            Description = description,
            Amount = amount,
            ExpenseDate = date
        });
        harness.Db.SaveChanges();
    }
}
