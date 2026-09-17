using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Tests;

public class StockSaleTests
{
    [Fact]
    public async Task SaveOrder_deducts_tracked_stock()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 8m);

        var result = await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails =
            [
                new OrderDetailViewModel { ProductId = product.Id, Quantity = 3m, UnitPrice = 999m }
            ]
        });

        Assert.IsType<OkObjectResult>(result);

        var stored = await harness.Db.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal(5m, stored.StockQuantity);

        var ledger = Assert.Single(await harness.Db.StockLedgers.ToListAsync());
        Assert.Equal(-3m, ledger.QuantityChange);
        Assert.Equal(8m, ledger.QuantityBefore);
        Assert.Equal(5m, ledger.QuantityAfter);
        Assert.Equal(StockMovementTypes.Sale, ledger.MovementType);
    }

    [Fact]
    public async Task SaveOrder_rejects_quantity_above_available_stock()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 2m);

        var result = await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails =
            [
                new OrderDetailViewModel { ProductId = product.Id, Quantity = 5m, UnitPrice = 10m }
            ]
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(harness.Db.Orders);
        Assert.Equal(2m, (await harness.Db.Products.SingleAsync()).StockQuantity);
        Assert.Empty(harness.Db.StockLedgers);
    }

    [Fact]
    public async Task SaveOrder_rejects_without_open_shift()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 8m);

        var result = await harness.ProductsController(seedShift: false).SaveOrder(new OrderViewModel
        {
            OrderDetails =
            [
                new OrderDetailViewModel { ProductId = product.Id, Quantity = 1m }
            ]
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(harness.Db.Orders);
        Assert.Equal(8m, (await harness.Db.Products.SingleAsync()).StockQuantity);
    }
}
