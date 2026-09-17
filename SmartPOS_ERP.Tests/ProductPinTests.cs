using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Tests;

public class ProductPinTests
{
    [Fact]
    public async Task Index_lists_pinned_products_first()
    {
        await using var harness = new PosHarness();
        var loose = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 5m);
        loose.Name = "أرز";
        var pinned = harness.SeedProduct(salePrice: 12m, costPrice: 5m, stock: 5m);
        pinned.Name = "زبادي";
        pinned.IsPinned = true;
        await harness.Db.SaveChangesAsync();

        var result = Assert.IsType<ViewResult>(await harness.ProductsController().Index());
        var products = Assert.IsAssignableFrom<IEnumerable<Product>>(result.Model).ToList();

        Assert.Equal([pinned.Id, loose.Id], products.Select(p => p.Id));
    }

    [Fact]
    public async Task TogglePin_flips_flag()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 5m);

        Assert.IsType<JsonResult>(await harness.ProductsController().TogglePin(product.Id));
        Assert.True((await harness.Db.Products.SingleAsync()).IsPinned);

        await harness.ProductsController().TogglePin(product.Id);
        Assert.False((await harness.Db.Products.SingleAsync()).IsPinned);
    }
}
