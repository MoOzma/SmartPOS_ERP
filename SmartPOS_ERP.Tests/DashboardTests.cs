using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SmartPOS_ERP.Controllers;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Tests;

public class DashboardTests
{
    [Fact]
    public async Task Empty_database_returns_zeros()
    {
        await using var harness = new PosHarness();
        var service = new DashboardService(harness.Db, NullLogger<DashboardService>.Instance, new ShiftCashService(harness.Db));
        var now = new DateTime(2026, 9, 14, 15, 0, 0);

        var model = await service.BuildAsync(now);

        Assert.Equal(0m, model.TodaySales);
        Assert.Equal(0m, model.MonthSales);
        Assert.Equal(0m, model.MonthProfit);
        Assert.Equal(0m, model.SupplierPayables);
        Assert.Equal(0m, model.MonthExpenses);
        Assert.Equal(0, model.MonthInvoiceCount);
        Assert.Equal(0m, model.StockValue);
        Assert.Equal(0m, model.CashBalance);
        Assert.Equal(0, model.MonthPurchaseCount);
        Assert.Equal(0, model.LowStockCount);
        Assert.Equal(12, model.MonthlySales.Count);
        Assert.Equal(24, model.HourlySales.Count);
        Assert.Empty(model.TopProducts);
        Assert.Empty(model.RecentInvoices);
        Assert.Empty(model.LowStock);
    }

    [Fact]
    public async Task Builds_sales_profit_payables_and_rankings()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 2m, taxRate: 0m);
        product.ReorderLevel = 5;
        harness.Db.SaveChanges();

        var now = new DateTime(2026, 9, 14, 16, 30, 0);
        harness.Db.Orders.Add(new Order
        {
            OrderDate = now,
            TotalAmount = 100m,
            TaxAmount = 0m,
            OrderDetails =
            [
                new OrderDetail
                {
                    ProductId = product.Id,
                    Quantity = 2m,
                    UnitPrice = 50m,
                    UnitCost = 20m
                }
            ]
        });
        harness.Db.Expenses.Add(new Expense
        {
            Description = "rent",
            Category = "إيجار",
            Amount = 10m,
            ExpenseDate = now.Date
        });
        var supplier = new Supplier { Name = "S1", Phone = "1", Invoices = [], Payments = [] };
        harness.Db.Suppliers.Add(supplier);
        harness.Db.SaveChanges();
        harness.Db.PurchaseInvoices.Add(new PurchaseInvoice
        {
            SupplierId = supplier.Id,
            InvoiceDate = now.Date,
            Details =
            [
                new PurchaseDetail
                {
                    ProductId = product.Id,
                    PackageQuantity = 2m,
                    PackageCost = 40m,
                    UnitsPerPackage = 1m,
                    TotalUnits = 2m,
                    UnitCost = 40m
                }
            ]
        });
        harness.Db.SupplierPayments.Add(new SupplierPayment
        {
            SupplierId = supplier.Id,
            AmountPaid = 30m,
            PaymentDate = now.Date
        });
        harness.Db.SaveChanges();

        var service = new DashboardService(harness.Db, NullLogger<DashboardService>.Instance, new ShiftCashService(harness.Db));
        var model = await service.BuildAsync(now);

        Assert.Equal(100m, model.TodaySales);
        Assert.Equal(100m, model.MonthSales);
        Assert.Equal(50m, model.MonthProfit);
        Assert.Equal(10m, model.MonthExpenses);
        Assert.Equal(50m, model.SupplierPayables);
        Assert.Equal(1, model.MonthInvoiceCount);
        Assert.Equal(1, model.MonthPurchaseCount);
        Assert.Equal(40m, model.StockValue);
        Assert.Equal(0m, model.CashBalance);
        Assert.Equal(1, model.LowStockCount);
        Assert.Equal(100m, model.YearSales);
        Assert.Equal(10m, model.YearExpenses);
        Assert.Equal(80m, model.YearPurchases);
        Assert.Equal(90m, model.YearOperatingNet);
        var september = Assert.Single(model.MonthlySales, x => x.Label == "2026-09");
        Assert.Equal(100m, september.Total);
        Assert.Equal(10m, september.Expenses);
        Assert.Equal(80m, september.Purchases);
        Assert.Equal("2026-01", model.MonthlySales[0].Label);
        Assert.Equal("2026-12", model.MonthlySales[11].Label);
        Assert.Equal(24, model.HourlySales.Count);
        Assert.Equal(100m, model.HourlySales[16].Total);
        var top = Assert.Single(model.TopProducts);
        Assert.Equal("Test product", top.Name);
        Assert.Equal(2m, top.Quantity);
        Assert.Single(model.RecentInvoices);
        Assert.Single(model.LowStock);
    }

    [Fact]
    public void Legacy_report_urls_redirect()
    {
        var reports = new ReportsController(null!, null!);
        var daily = Assert.IsType<RedirectToActionResult>(reports.DailyReport());
        Assert.Equal(nameof(ReportsController.Index), daily.ActionName);
        Assert.Equal("Dashboard", Assert.IsType<RedirectToActionResult>(reports.TopFive()).ControllerName);
    }
}
