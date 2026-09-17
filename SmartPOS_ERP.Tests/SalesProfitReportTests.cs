using Microsoft.AspNetCore.Mvc;
using SmartPOS_ERP.Controllers;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Tests;

public class SalesProfitReportTests
{
    [Fact]
    public async Task Day_report_includes_only_that_days_sales_profit_and_expenses()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m, taxRate: 0m);
        SeedSale(harness, product.Id, new DateTime(2026, 9, 14, 10, 0, 0), quantity: 2m, unitPrice: 50m, unitCost: 20m, total: 100m);
        SeedSale(harness, product.Id, new DateTime(2026, 9, 15, 10, 0, 0), quantity: 1m, unitPrice: 50m, unitCost: 20m, total: 50m);
        harness.Db.Expenses.Add(new Expense
        {
            Description = "rent",
            Category = "إيجار",
            Amount = 10m,
            ExpenseDate = new DateTime(2026, 9, 14)
        });
        harness.Db.Expenses.Add(new Expense
        {
            Description = "power",
            Category = "كهرباء",
            Amount = 40m,
            ExpenseDate = new DateTime(2026, 9, 15)
        });
        harness.Db.SaveChanges();

        var model = await new SalesProfitReportService(harness.Db)
            .BuildAsync("day", new DateTime(2026, 9, 14), null, null, new DateTime(2026, 9, 16));

        Assert.Equal("day", model.PeriodKind);
        Assert.Equal(100m, model.GrossSales);
        Assert.Equal(0m, model.Returns);
        Assert.Equal(100m, model.NetSales);
        Assert.Equal(60m, model.GrossProfit);
        Assert.Equal(10m, model.Expenses);
        Assert.Equal(50m, model.NetProfit);
        Assert.Equal(1, model.InvoiceCount);
        Assert.Single(model.Invoices);
    }

    [Fact]
    public async Task Month_report_includes_whole_month_and_excludes_other_months()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m, taxRate: 0m);
        SeedSale(harness, product.Id, new DateTime(2026, 9, 1, 9, 0, 0), quantity: 1m, unitPrice: 50m, unitCost: 20m, total: 50m);
        SeedSale(harness, product.Id, new DateTime(2026, 9, 30, 18, 0, 0), quantity: 2m, unitPrice: 50m, unitCost: 20m, total: 100m);
        SeedSale(harness, product.Id, new DateTime(2026, 10, 1, 9, 0, 0), quantity: 1m, unitPrice: 50m, unitCost: 20m, total: 50m);
        harness.Db.Expenses.Add(new Expense
        {
            Description = "rent",
            Category = "إيجار",
            Amount = 20m,
            ExpenseDate = new DateTime(2026, 9, 20)
        });
        harness.Db.SaveChanges();

        var model = await new SalesProfitReportService(harness.Db)
            .BuildAsync("month", null, 9, 2026, new DateTime(2026, 10, 5));

        Assert.Equal("month", model.PeriodKind);
        Assert.Equal(150m, model.GrossSales);
        Assert.Equal(90m, model.GrossProfit);
        Assert.Equal(20m, model.Expenses);
        Assert.Equal(70m, model.NetProfit);
        Assert.Equal(2, model.InvoiceCount);
    }

    [Fact]
    public async Task Range_report_includes_only_dates_from_to()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 20m, taxRate: 0m);
        SeedSale(harness, product.Id, new DateTime(2026, 9, 13, 10, 0, 0), quantity: 1m, unitPrice: 50m, unitCost: 20m, total: 50m);
        SeedSale(harness, product.Id, new DateTime(2026, 9, 14, 10, 0, 0), quantity: 1m, unitPrice: 50m, unitCost: 20m, total: 50m);
        SeedSale(harness, product.Id, new DateTime(2026, 9, 16, 10, 0, 0), quantity: 2m, unitPrice: 50m, unitCost: 20m, total: 100m);
        SeedSale(harness, product.Id, new DateTime(2026, 9, 18, 10, 0, 0), quantity: 1m, unitPrice: 50m, unitCost: 20m, total: 50m);
        harness.Db.Expenses.Add(new Expense
        {
            Description = "power",
            Category = "كهرباء",
            Amount = 15m,
            ExpenseDate = new DateTime(2026, 9, 15)
        });
        harness.Db.SaveChanges();

        var model = await new SalesProfitReportService(harness.Db)
            .BuildAsync(
                "range",
                null,
                null,
                null,
                new DateTime(2026, 9, 20),
                new DateTime(2026, 9, 14),
                new DateTime(2026, 9, 16));

        Assert.Equal("range", model.PeriodKind);
        Assert.Equal(150m, model.GrossSales);
        Assert.Equal(90m, model.GrossProfit);
        Assert.Equal(15m, model.Expenses);
        Assert.Equal(75m, model.NetProfit);
        Assert.Equal(2, model.InvoiceCount);
        Assert.Equal(new DateTime(2026, 9, 14), model.FromDate);
        Assert.Equal(new DateTime(2026, 9, 16), model.ToDate);
    }

    [Fact]
    public async Task Returns_in_period_reduce_sales_and_profit()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m, taxRate: 0m);
        var order = SeedSale(harness, product.Id, new DateTime(2026, 9, 14, 10, 0, 0), quantity: 2m, unitPrice: 50m, unitCost: 20m, total: 100m);
        order.TotalAmount = 50m;
        order.OrderDetails[0].Quantity = 1m;
        harness.Db.SalesReturns.Add(new SalesReturn
        {
            OrderId = order.Id,
            ProductId = product.Id,
            Quantity = 1m,
            RefundAmount = 50m,
            ReturnDate = new DateTime(2026, 9, 14, 16, 0, 0)
        });
        harness.Db.SaveChanges();

        var model = await new SalesProfitReportService(harness.Db)
            .BuildAsync("day", new DateTime(2026, 9, 14), null, null, new DateTime(2026, 9, 14));

        Assert.Equal(100m, model.GrossSales);
        Assert.Equal(50m, model.Returns);
        Assert.Equal(50m, model.NetSales);
        Assert.Equal(30m, model.GrossProfit);
        Assert.Equal(30m, model.NetProfit);
        Assert.Equal(1, model.ReturnCount);
    }

    [Fact]
    public async Task Unpaid_credit_is_excluded_until_an_order_exists()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 8m);
        harness.SeedOpenShift();
        await harness.CreditInvoices().CreateAsync(
            "cashier",
            "عميل آجل",
            null,
            [new CreditLineInput { ProductId = product.Id, Quantity = 2m }]);

        var model = await new SalesProfitReportService(harness.Db)
            .BuildAsync("day", DateTime.Now, null, null, DateTime.Now);

        Assert.Equal(0m, model.GrossSales);
        Assert.Equal(0m, model.NetProfit);
        Assert.Equal(0, model.InvoiceCount);
    }

    [Fact]
    public async Task Index_returns_the_selected_period_report()
    {
        await using var harness = new PosHarness();
        var controller = new ReportsController(new SalesProfitReportService(harness.Db), harness.Db);
        var result = await controller.Index("month", null, 9, 2026);
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SalesProfitReportViewModel>(view.Model);
        Assert.Equal("month", model.PeriodKind);
        Assert.Equal(9, model.SelectedMonth);
        Assert.Equal(2026, model.SelectedYear);
    }

    [Fact]
    public async Task Index_returns_the_selected_range_report()
    {
        await using var harness = new PosHarness();
        var controller = new ReportsController(new SalesProfitReportService(harness.Db), harness.Db);
        var result = await controller.Index("range", null, null, null, new DateTime(2026, 9, 14), new DateTime(2026, 9, 16));
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SalesProfitReportViewModel>(view.Model);
        Assert.Equal("range", model.PeriodKind);
        Assert.Equal(new DateTime(2026, 9, 14), model.FromDate);
        Assert.Equal(new DateTime(2026, 9, 16), model.ToDate);
    }

    private static Order SeedSale(
        PosHarness harness,
        int productId,
        DateTime date,
        decimal quantity,
        decimal unitPrice,
        decimal unitCost,
        decimal total)
    {
        var order = new Order
        {
            OrderDate = date,
            TotalAmount = total,
            TaxAmount = 0m,
            OrderDetails =
            [
                new OrderDetail
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    UnitCost = unitCost
                }
            ]
        };
        harness.Db.Orders.Add(order);
        harness.Db.SaveChanges();
        return order;
    }
}
