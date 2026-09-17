using Microsoft.AspNetCore.Mvc;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Tests;

public class SupplierDirectoryTests
{
    [Fact]
    public async Task Directory_totals_purchases_payments_and_last_invoice()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 5m, stock: 0m);
        var due = SeedSupplier(harness, "شركة النور", "0100", "القاهرة", "أجل 30 يوم");
        var settled = SeedSupplier(harness, "مورد مسدد", "0111", null, null);
        SeedInvoice(harness, due.Id, product.Id, new DateTime(2026, 9, 10), 2m, 50m);
        SeedInvoice(harness, due.Id, product.Id, new DateTime(2026, 9, 20), 1m, 20m);
        SeedPayment(harness, due.Id, 40m);
        SeedInvoice(harness, settled.Id, product.Id, new DateTime(2026, 8, 1), 1m, 30m);
        SeedPayment(harness, settled.Id, 30m);

        var model = await new SupplierDirectoryService(harness.Db).BuildAsync(null, "all");

        Assert.Equal(2, model.SupplierCount);
        Assert.Equal(1, model.DueCount);
        Assert.Equal(150m, model.TotalPurchases);
        Assert.Equal(70m, model.TotalPaid);
        Assert.Equal(80m, model.TotalBalance);
        var row = Assert.Single(model.Items, x => x.Name == "شركة النور");
        Assert.Equal("0100", row.Phone);
        Assert.Equal("القاهرة", row.Address);
        Assert.Equal("أجل 30 يوم", row.Notes);
        Assert.Equal(2, row.InvoiceCount);
        Assert.Equal(new DateTime(2026, 9, 20), row.LastInvoiceDate);
        Assert.Equal(120m, row.TotalPurchases);
        Assert.Equal(40m, row.TotalPaid);
        Assert.Equal(80m, row.Balance);
        Assert.Equal("شركة النور", model.Items[0].Name);
    }

    [Fact]
    public async Task Directory_search_matches_name_or_phone()
    {
        await using var harness = new PosHarness();
        SeedSupplier(harness, "شركة النور", "0100", "القاهرة", null);
        SeedSupplier(harness, "مورد آخر", "0111", null, null);

        var byName = await new SupplierDirectoryService(harness.Db).BuildAsync("النور", "all");
        Assert.Equal("شركة النور", Assert.Single(byName.Items).Name);

        var byPhone = await new SupplierDirectoryService(harness.Db).BuildAsync("0111", "all");
        Assert.Equal("مورد آخر", Assert.Single(byPhone.Items).Name);
    }

    [Fact]
    public async Task Directory_due_filter_hides_settled_suppliers()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 5m, stock: 0m);
        var due = SeedSupplier(harness, "مدين", null, null, null);
        var settled = SeedSupplier(harness, "مسدد", null, null, null);
        SeedInvoice(harness, due.Id, product.Id, DateTime.Today, 1m, 50m);
        SeedInvoice(harness, settled.Id, product.Id, DateTime.Today, 1m, 20m);
        SeedPayment(harness, settled.Id, 20m);

        var model = await new SupplierDirectoryService(harness.Db).BuildAsync(null, "due");
        Assert.Equal("مدين", Assert.Single(model.Items).Name);
        Assert.Equal(1, model.SupplierCount);
        Assert.Equal(1, model.DueCount);
        Assert.Equal(50m, model.TotalBalance);
    }

    [Fact]
    public async Task Suppliers_action_returns_filtered_directory()
    {
        await using var harness = new PosHarness();
        SeedSupplier(harness, "شركة النور", "0100", null, null);
        SeedSupplier(harness, "مورد آخر", "0111", null, null);

        var result = await harness.PurchasesController().Suppliers("النور", "all");
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SupplierDirectoryViewModel>(view.Model);
        Assert.Equal("النور", model.Query);
        Assert.Equal("شركة النور", Assert.Single(model.Items).Name);
    }

    private static Supplier SeedSupplier(PosHarness harness, string name, string? phone, string? address, string? notes)
    {
        var supplier = new Supplier
        {
            Name = name,
            Phone = phone,
            Address = address,
            Notes = notes,
            Invoices = [],
            Payments = []
        };
        harness.Db.Suppliers.Add(supplier);
        harness.Db.SaveChanges();
        return supplier;
    }

    private static void SeedInvoice(PosHarness harness, int supplierId, int productId, DateTime date, decimal packages, decimal packageCost)
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
                    TotalUnits = packages,
                    PackageCost = packageCost,
                    UnitCost = packageCost
                }
            ]
        });
        harness.Db.SaveChanges();
    }

    private static void SeedPayment(PosHarness harness, int supplierId, decimal amount)
    {
        harness.Db.SupplierPayments.Add(new SupplierPayment
        {
            SupplierId = supplierId,
            AmountPaid = amount,
            PaymentDate = DateTime.Today
        });
        harness.Db.SaveChanges();
    }
}
