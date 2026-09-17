using Microsoft.AspNetCore.Mvc;
using SmartPOS_ERP.Controllers;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Tests;

public class LowStockReportTests
{
    [Fact]
    public async Task Lists_tracked_items_at_or_below_reorder()
    {
        await using var harness = new PosHarness();
        var low = harness.SeedProduct(salePrice: 10m, costPrice: 5m, stock: 2m);
        low.Name = "Low item";
        low.ReorderLevel = 5;
        var empty = harness.SeedProduct(salePrice: 10m, costPrice: 5m, stock: 0m);
        empty.Name = "Empty";
        empty.ReorderLevel = 1;
        var plenty = harness.SeedProduct(salePrice: 10m, costPrice: 5m, stock: 20m);
        plenty.Name = "Plenty";
        plenty.ReorderLevel = 5;
        var untracked = harness.SeedProduct(salePrice: 10m, costPrice: 5m, stock: 0m, trackInventory: false);
        untracked.Name = "Service";
        untracked.ReorderLevel = 1;
        harness.Db.SaveChanges();

        var controller = new ReportsController(new SalesProfitReportService(harness.Db), harness.Db);
        var result = await controller.LowStockReport();

        var view = Assert.IsType<ViewResult>(result);
        var items = Assert.IsAssignableFrom<IEnumerable<Product>>(view.Model).Select(p => p.Name).ToList();
        Assert.Equal(["Empty", "Low item"], items);
    }
}
