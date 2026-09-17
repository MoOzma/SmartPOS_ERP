using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Tests;

public class SaveOrderPricingTests
{
    [Fact]
    public async Task SaveOrder_ignores_client_unit_price_and_total()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m, taxRate: 0m);

        var result = await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            TotalAmount = 1m,
            TaxAmount = 99m,
            OrderDetails =
            [
                new OrderDetailViewModel
                {
                    ProductId = product.Id,
                    Quantity = 2m,
                    UnitPrice = 1m
                }
            ]
        });

        Assert.IsType<OkObjectResult>(result);

        var order = await harness.Db.Orders.Include(o => o.OrderDetails).SingleAsync();
        var line = Assert.Single(order.OrderDetails);

        Assert.Equal(50m, line.UnitPrice);
        Assert.Equal(20m, line.UnitCost);
        Assert.Equal(100m, order.TotalAmount);
        Assert.Equal(0m, order.TaxAmount);
    }

    [Fact]
    public async Task SaveOrder_adds_server_tax_to_total()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m, taxRate: 14m);

        var result = await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            TotalAmount = 0m,
            TaxAmount = 0m,
            OrderDetails =
            [
                new OrderDetailViewModel
                {
                    ProductId = product.Id,
                    Quantity = 2m,
                    UnitPrice = 1m
                }
            ]
        });

        Assert.IsType<OkObjectResult>(result);

        var order = await harness.Db.Orders.SingleAsync();
        Assert.Equal(14m, order.TaxAmount);
        Assert.Equal(114m, order.TotalAmount);
    }

    [Fact]
    public async Task SaveOrder_skips_tax_when_disabled()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m, taxRate: 14m);
        var settings = await harness.StoreSettings().GetAsync();
        settings.TaxEnabled = false;
        await harness.Db.SaveChangesAsync();

        var result = await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails =
            [
                new OrderDetailViewModel { ProductId = product.Id, Quantity = 2m, UnitPrice = 1m }
            ]
        });

        Assert.IsType<OkObjectResult>(result);
        var order = await harness.Db.Orders.Include(o => o.OrderDetails).SingleAsync();
        Assert.Equal(0m, order.TaxAmount);
        Assert.Equal(100m, order.TotalAmount);
        Assert.Equal(0m, Assert.Single(order.OrderDetails).TaxRate);
    }

    [Fact]
    public async Task SaveOrder_uses_default_rate_when_product_has_no_override()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m, taxRate: 0m);
        var settings = await harness.StoreSettings().GetAsync();
        settings.DefaultTaxRate = 14m;
        await harness.Db.SaveChangesAsync();

        var result = await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails =
            [
                new OrderDetailViewModel { ProductId = product.Id, Quantity = 2m, UnitPrice = 1m }
            ]
        });

        Assert.IsType<OkObjectResult>(result);
        var order = await harness.Db.Orders.Include(o => o.OrderDetails).SingleAsync();
        Assert.Equal(14m, order.TaxAmount);
        Assert.Equal(114m, order.TotalAmount);
        Assert.Equal(14m, Assert.Single(order.OrderDetails).TaxRate);
    }

    [Fact]
    public async Task SaveOrder_uses_custom_zero_instead_of_default()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m, taxRate: 0m);
        product.UseCustomTax = true;
        var settings = await harness.StoreSettings().GetAsync();
        settings.DefaultTaxRate = 14m;
        await harness.Db.SaveChangesAsync();

        var result = await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails =
            [
                new OrderDetailViewModel { ProductId = product.Id, Quantity = 2m, UnitPrice = 1m }
            ]
        });

        Assert.IsType<OkObjectResult>(result);
        var order = await harness.Db.Orders.Include(o => o.OrderDetails).SingleAsync();
        Assert.Equal(0m, order.TaxAmount);
        Assert.Equal(100m, order.TotalAmount);
        Assert.Equal(0m, Assert.Single(order.OrderDetails).TaxRate);
    }
}
