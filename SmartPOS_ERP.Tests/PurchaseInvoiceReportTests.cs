using Microsoft.AspNetCore.Mvc;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Tests;

public class PurchaseInvoiceReportTests
{
    [Fact]
    public async Task Day_report_includes_only_that_days_invoices()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 5m, stock: 0m);
        var light = SeedSupplier(harness, "شركة النور");
        var other = SeedSupplier(harness, "مورد آخر");
        SeedInvoice(harness, light.Id, product.Id, new DateTime(2026, 9, 14), 2m, 50m, 2m);
        SeedInvoice(harness, other.Id, product.Id, new DateTime(2026, 9, 15), 1m, 20m, 1m);

        var model = await new PurchaseInvoiceReportService(harness.Db)
            .BuildAsync("day", new DateTime(2026, 9, 14), null, null, new DateTime(2026, 9, 14));

        Assert.Equal("day", model.PeriodKind);
        Assert.Equal(100m, model.TotalAmount);
        Assert.Equal(1, model.InvoiceCount);
        Assert.Equal(1, model.SupplierCount);
        Assert.Equal(2m, model.TotalUnits);
        Assert.Equal("شركة النور", model.TopSupplier);
        Assert.Equal("شركة النور", Assert.Single(model.Items).SupplierName);
        Assert.Equal(100m, Assert.Single(model.Suppliers).Amount);
    }

    [Fact]
    public async Task Month_report_includes_the_whole_month()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 5m, stock: 0m);
        var supplier = SeedSupplier(harness, "شركة النور");
        SeedInvoice(harness, supplier.Id, product.Id, new DateTime(2026, 9, 2), 1m, 80m, 1m);
        SeedInvoice(harness, supplier.Id, product.Id, new DateTime(2026, 9, 28), 1m, 20m, 1m);
        SeedInvoice(harness, supplier.Id, product.Id, new DateTime(2026, 8, 31), 1m, 50m, 1m);

        var model = await new PurchaseInvoiceReportService(harness.Db)
            .BuildAsync("month", null, 9, 2026, new DateTime(2026, 9, 16));

        Assert.Equal("month", model.PeriodKind);
        Assert.Equal(100m, model.TotalAmount);
        Assert.Equal(2, model.InvoiceCount);
        Assert.DoesNotContain(model.Items, x => x.TotalAmount == 50m);
    }

    [Fact]
    public async Task Search_matches_supplier_name()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 5m, stock: 0m);
        var light = SeedSupplier(harness, "شركة النور");
        var other = SeedSupplier(harness, "مورد آخر");
        SeedInvoice(harness, light.Id, product.Id, new DateTime(2026, 9, 10), 1m, 40m, 1m);
        SeedInvoice(harness, other.Id, product.Id, new DateTime(2026, 9, 10), 1m, 15m, 1m);

        var model = await new PurchaseInvoiceReportService(harness.Db)
            .BuildAsync("month", null, 9, 2026, new DateTime(2026, 9, 16), query: "النور");

        Assert.Equal("شركة النور", Assert.Single(model.Items).SupplierName);
        Assert.Equal(40m, model.TotalAmount);
    }

    [Fact]
    public async Task Index_returns_the_selected_period_report()
    {
        await using var harness = new PosHarness();
        var result = await harness.PurchasesController().Index("month", null, 9, 2026);
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PurchaseInvoiceReportViewModel>(view.Model);
        Assert.Equal("month", model.PeriodKind);
        Assert.Equal(9, model.SelectedMonth);
        Assert.Equal(2026, model.SelectedYear);
    }

    private static Supplier SeedSupplier(PosHarness harness, string name)
    {
        var supplier = new Supplier { Name = name, Invoices = [], Payments = [] };
        harness.Db.Suppliers.Add(supplier);
        harness.Db.SaveChanges();
        return supplier;
    }

    private static void SeedInvoice(
        PosHarness harness,
        int supplierId,
        int productId,
        DateTime date,
        decimal packages,
        decimal packageCost,
        decimal totalUnits)
    {
        harness.Db.PurchaseInvoices.Add(new PurchaseInvoice
        {
            SupplierId = supplierId,
            InvoiceDate = date,
            Details =
            [
                new PurchaseDetail
                {
                    ProductId = productId,
                    PackageQuantity = packages,
                    UnitsPerPackage = 1m,
                    TotalUnits = totalUnits,
                    PackageCost = packageCost,
                    UnitCost = packageCost
                }
            ]
        });
        harness.Db.SaveChanges();
    }
}
