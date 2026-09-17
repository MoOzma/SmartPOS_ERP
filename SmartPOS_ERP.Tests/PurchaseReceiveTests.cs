using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Tests;

public class PurchaseReceiveTests
{
    [Fact]
    public async Task SavePurchase_averages_cost_updates_sale_price_and_returns_id()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 10m);
        harness.SeedOpenShift();
        var supplier = new Supplier { Name = "مورد", Invoices = [], Payments = [] };
        harness.Db.Suppliers.Add(supplier);
        await harness.Db.SaveChangesAsync();

        var result = await harness.PurchasesController().SavePurchase(new PurchaseViewModel
        {
            SupplierId = supplier.Id,
            PurchaseDate = DateTime.Today,
            Items =
            [
                new PurchaseItemViewModel
                {
                    ProductId = product.Id,
                    PackageQuantity = 10m,
                    UnitsPerPackage = 1m,
                    PackageCost = 6m,
                    NewSalePrice = 12m
                }
            ]
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.True(doc.RootElement.GetProperty("id").GetInt32() > 0);

        var saved = await harness.Db.Products.SingleAsync();
        Assert.Equal(20m, saved.StockQuantity);
        Assert.Equal(5m, saved.CostPrice);
        Assert.Equal(12m, saved.SalePrice);

        var detail = (await harness.Db.PurchaseInvoices.Include(i => i.Details).SingleAsync()).Details.Single();
        Assert.Equal(6m, detail.PackageCost);
        Assert.Equal(6m, detail.UnitCost);
    }

    [Fact]
    public async Task SavePurchase_uses_piece_package_cost_for_unit_cost()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 0m);
        harness.SeedOpenShift();
        var supplier = new Supplier { Name = "مورد", Invoices = [], Payments = [] };
        harness.Db.Suppliers.Add(supplier);
        await harness.Db.SaveChangesAsync();

        await harness.PurchasesController().SavePurchase(new PurchaseViewModel
        {
            SupplierId = supplier.Id,
            Items =
            [
                new PurchaseItemViewModel
                {
                    ProductId = product.Id,
                    PackageQuantity = 2m,
                    UnitsPerPackage = 5m,
                    PackageCost = 50m
                }
            ]
        });

        var saved = await harness.Db.Products.SingleAsync();
        Assert.Equal(10m, saved.StockQuantity);
        Assert.Equal(10m, saved.CostPrice);
        Assert.Equal(10m, saved.SalePrice);
        var detail = (await harness.Db.PurchaseInvoices.Include(i => i.Details).SingleAsync()).Details.Single();
        Assert.Equal(50m, detail.PackageCost);
        Assert.Equal(10m, detail.UnitCost);
    }
}
