using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Tests;

public class SalesReturnTests
{
    [Fact]
    public async Task ProcessReturn_restores_stock_and_reduces_order_totals()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m, taxRate: 14m);

        var sale = await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails =
            [
                new OrderDetailViewModel { ProductId = product.Id, Quantity = 2m, UnitPrice = 1m }
            ]
        });
        Assert.IsType<OkObjectResult>(sale);

        var order = await harness.Db.Orders.Include(o => o.OrderDetails).SingleAsync();
        Assert.Equal(114m, order.TotalAmount);

        var result = await harness.OrderController().ProcessReturn(order.Id, product.Id, 1m);
        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(true, ReadAnonymous(json, "success"));

        await harness.Db.Entry(order).ReloadAsync();
        var line = await harness.Db.OrderDetails.SingleAsync();
        var storedProduct = await harness.Db.Products.SingleAsync();
        var salesReturn = await harness.Db.SalesReturns.SingleAsync();

        Assert.Equal(1m, line.Quantity);
        Assert.Equal(57m, order.TotalAmount);
        Assert.Equal(7m, order.TaxAmount);
        Assert.Equal(9m, storedProduct.StockQuantity);
        Assert.Equal(57m, salesReturn.RefundAmount);

        var movements = await harness.Db.StockLedgers.OrderBy(l => l.Id).ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Equal(StockMovementTypes.Return, movements[1].MovementType);
        Assert.Equal(1m, movements[1].QuantityChange);
    }

    [Fact]
    public async Task ProcessReturn_uses_line_tax_rate_after_settings_change()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m, taxRate: 14m);

        var sale = await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails =
            [
                new OrderDetailViewModel { ProductId = product.Id, Quantity = 2m, UnitPrice = 1m }
            ]
        });
        Assert.IsType<OkObjectResult>(sale);

        product.TaxRate = 0m;
        product.UseCustomTax = false;
        var settings = await harness.StoreSettings().GetAsync();
        settings.TaxEnabled = false;
        await harness.Db.SaveChangesAsync();

        var order = await harness.Db.Orders.Include(o => o.OrderDetails).SingleAsync();
        var result = await harness.OrderController().ProcessReturn(order.Id, product.Id, 1m);
        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(true, ReadAnonymous(json, "success"));

        await harness.Db.Entry(order).ReloadAsync();
        Assert.Equal(57m, order.TotalAmount);
        Assert.Equal(7m, order.TaxAmount);
    }

    [Fact]
    public async Task ProcessReturn_rejects_quantity_above_sold()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 5m);

        await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails =
            [
                new OrderDetailViewModel { ProductId = product.Id, Quantity = 2m, UnitPrice = 10m }
            ]
        });

        var order = await harness.Db.Orders.SingleAsync();
        var result = await harness.OrderController().ProcessReturn(order.Id, product.Id, 3m);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(false, ReadAnonymous(json, "success"));
        Assert.Empty(harness.Db.SalesReturns);
        Assert.Equal(3m, (await harness.Db.Products.SingleAsync()).StockQuantity);
        Assert.Equal(2m, (await harness.Db.OrderDetails.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task ProcessReturn_updates_daily_report_and_profit()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m, taxRate: 0m);

        await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails =
            [
                new OrderDetailViewModel { ProductId = product.Id, Quantity = 2m, UnitPrice = 1m }
            ]
        });

        var order = await harness.Db.Orders.SingleAsync();
        var afterSale = await harness.Dashboard().BuildAsync(order.OrderDate);
        Assert.Equal(100m, afterSale.TodaySales);
        Assert.Equal(100m, afterSale.MonthSales);
        Assert.Equal(60m, afterSale.MonthProfit);

        await harness.OrderController().ProcessReturn(order.Id, product.Id, 1m);

        var afterReturn = await harness.Dashboard().BuildAsync(order.OrderDate);
        Assert.Equal(50m, afterReturn.TodaySales);
        Assert.Equal(50m, afterReturn.MonthSales);
        Assert.Equal(30m, afterReturn.MonthProfit);

        var report = await new SalesProfitReportService(harness.Db)
            .BuildAsync("day", order.OrderDate, null, null, order.OrderDate);
        Assert.Equal(100m, report.GrossSales);
        Assert.Equal(50m, report.Returns);
        Assert.Equal(50m, report.NetSales);
        Assert.Equal(30m, report.GrossProfit);
        Assert.Equal(30m, report.NetProfit);
    }

    [Fact]
    public async Task ProcessReturn_rejects_without_open_shift()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 5m);
        await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails = [new OrderDetailViewModel { ProductId = product.Id, Quantity = 2m }]
        });

        var shift = await harness.Db.Shifts.SingleAsync();
        shift.ClosedAt = DateTime.Now;
        await harness.Db.SaveChangesAsync();

        var order = await harness.Db.Orders.SingleAsync();
        var result = await harness.OrderController().ProcessReturn(order.Id, product.Id, 1m);
        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(false, ReadAnonymous(json, "success"));
        Assert.Empty(harness.Db.SalesReturns);
        Assert.Equal(2m, (await harness.Db.OrderDetails.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task ProcessReturn_saves_reason_and_notes()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 5m);
        await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails = [new OrderDetailViewModel { ProductId = product.Id, Quantity = 2m }]
        });

        var order = await harness.Db.Orders.SingleAsync();
        var result = await harness.OrderController().ProcessReturn(
            order.Id, product.Id, 1m, "خطأ في البيع", "الباركود اتقرأ مرتين");

        Assert.Equal(true, ReadAnonymous(Assert.IsType<JsonResult>(result), "success"));
        var salesReturn = await harness.Db.SalesReturns.SingleAsync();
        Assert.Equal("خطأ في البيع", salesReturn.Reason);
        Assert.Equal("الباركود اتقرأ مرتين", salesReturn.Notes);
    }

    [Fact]
    public async Task ProcessReturn_rejects_fractional_quantity_for_piece()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 5m, unit: "Piece");
        await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails = [new OrderDetailViewModel { ProductId = product.Id, Quantity = 2m }]
        });

        var order = await harness.Db.Orders.SingleAsync();
        var result = await harness.OrderController().ProcessReturn(order.Id, product.Id, 1.5m);

        Assert.Equal(false, ReadAnonymous(Assert.IsType<JsonResult>(result), "success"));
        Assert.Empty(harness.Db.SalesReturns);
        Assert.Equal(2m, (await harness.Db.OrderDetails.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task ProcessReturn_allows_fractional_quantity_for_kilo()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 20m, costPrice: 8m, stock: 5m, unit: "Kilo");
        await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails = [new OrderDetailViewModel { ProductId = product.Id, Quantity = 1.5m }]
        });

        var order = await harness.Db.Orders.SingleAsync();
        var result = await harness.OrderController().ProcessReturn(order.Id, product.Id, 0.5m);

        Assert.Equal(true, ReadAnonymous(Assert.IsType<JsonResult>(result), "success"));
        Assert.Equal(1m, (await harness.Db.OrderDetails.SingleAsync()).Quantity);
        Assert.Equal(0.5m, (await harness.Db.SalesReturns.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task ProcessReturn_rejects_unknown_reason()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 5m);
        await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails = [new OrderDetailViewModel { ProductId = product.Id, Quantity = 1m }]
        });

        var order = await harness.Db.Orders.SingleAsync();
        var result = await harness.OrderController().ProcessReturn(order.Id, product.Id, 1m, "سبب مخترع");

        Assert.Equal(false, ReadAnonymous(Assert.IsType<JsonResult>(result), "success"));
        Assert.Empty(harness.Db.SalesReturns);
    }

    private static object? ReadAnonymous(JsonResult json, string propertyName)
    {
        var value = json.Value ?? throw new InvalidOperationException("JSON result has no value.");
        return value.GetType().GetProperty(propertyName)?.GetValue(value);
    }
}
