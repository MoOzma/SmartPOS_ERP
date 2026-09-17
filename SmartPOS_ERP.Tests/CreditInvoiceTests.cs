using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Tests;

public class CreditInvoiceTests
{
    [Fact]
    public async Task Create_deducts_stock_without_recording_a_sale()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 8m);
        harness.SeedOpenShift();

        var result = await harness.CreditInvoices().CreateAsync(
            "cashier",
            "عميل آجل",
            null,
            [new CreditLineInput { ProductId = product.Id, Quantity = 2m }]);

        Assert.True(result.Success);
        Assert.Empty(harness.Db.Orders);
        Assert.Equal(6m, (await harness.Db.Products.SingleAsync()).StockQuantity);
        Assert.Equal(20m, (await harness.Db.CreditInvoices.SingleAsync()).TotalAmount);
        Assert.Equal(0m, (await harness.Dashboard().BuildAsync(DateTime.Now)).TodaySales);
    }

    [Fact]
    public async Task Create_rejects_without_open_shift()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 8m);

        var result = await harness.CreditInvoices().CreateAsync(
            "cashier",
            "عميل",
            null,
            [new CreditLineInput { ProductId = product.Id, Quantity = 1m }]);

        Assert.False(result.Success);
        Assert.Empty(harness.Db.CreditInvoices);
        Assert.Equal(8m, (await harness.Db.Products.SingleAsync()).StockQuantity);
    }

    [Fact]
    public async Task Partial_payment_does_not_create_order()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m);
        harness.SeedOpenShift();
        var created = await harness.CreditInvoices().CreateAsync(
            "cashier", "أحمد", "0100",
            [new CreditLineInput { ProductId = product.Id, Quantity = 2m }]);

        var pay = await harness.CreditInvoices().RecordPaymentAsync("cashier", created.InvoiceId!.Value, 30m);

        Assert.True(pay.Success);
        Assert.False(pay.Settled);
        Assert.Empty(harness.Db.Orders);
        var invoice = await harness.Db.CreditInvoices.Include(i => i.Payments).SingleAsync();
        Assert.Equal(30m, invoice.PaidAmount);
        Assert.Equal(70m, invoice.RemainingAmount);
    }

    [Fact]
    public async Task Full_payment_creates_sale_with_original_date_without_double_stock()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 5m);
        harness.SeedOpenShift();
        var createdAt = DateTime.Now.AddDays(-2);
        var created = await harness.CreditInvoices().CreateAsync(
            "cashier", "منى", null,
            [new CreditLineInput { ProductId = product.Id, Quantity = 1m }]);

        var invoice = await harness.Db.CreditInvoices.SingleAsync();
        invoice.CreatedAt = createdAt;
        await harness.Db.SaveChangesAsync();

        var pay = await harness.CreditInvoices().RecordPaymentAsync("cashier", created.InvoiceId!.Value, 10m);

        Assert.True(pay.Settled);
        Assert.NotNull(pay.OrderId);
        var order = await harness.Db.Orders.Include(o => o.OrderDetails).SingleAsync();
        Assert.Equal(createdAt, order.OrderDate);
        Assert.True(order.IsCreditSettlement);
        Assert.Equal(10m, order.TotalAmount);
        Assert.Equal(4m, (await harness.Db.Products.SingleAsync()).StockQuantity);
        Assert.Single(await harness.Db.StockLedgers.ToListAsync());
    }

    [Fact]
    public async Task Add_line_increases_remaining_and_deducts_stock()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 10m);
        harness.SeedOpenShift();
        var created = await harness.CreditInvoices().CreateAsync(
            "cashier", "عميل", null,
            [new CreditLineInput { ProductId = product.Id, Quantity = 1m }]);

        var added = await harness.CreditInvoices().AddLineAsync("cashier", created.InvoiceId!.Value, product.Id, 2m);

        Assert.True(added.Success);
        Assert.Equal(7m, (await harness.Db.Products.SingleAsync()).StockQuantity);
        var invoice = await harness.Db.CreditInvoices.Include(i => i.Details).SingleAsync();
        Assert.Equal(3m, invoice.Details.Single().Quantity);
        Assert.Equal(30m, invoice.TotalAmount);
    }

    [Fact]
    public async Task Return_restores_stock_and_reduces_total()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 10m);
        harness.SeedOpenShift();
        var created = await harness.CreditInvoices().CreateAsync(
            "cashier", "عميل", null,
            [new CreditLineInput { ProductId = product.Id, Quantity = 3m }]);

        var returned = await harness.CreditInvoices().ReturnLineAsync(
            "cashier", created.InvoiceId!.Value, product.Id, 1m);

        Assert.True(returned.Success);
        Assert.Equal(8m, (await harness.Db.Products.SingleAsync()).StockQuantity);
        Assert.Equal(20m, (await harness.Db.CreditInvoices.Include(i => i.Details).SingleAsync()).TotalAmount);
        Assert.Empty(harness.Db.Orders);
    }

    [Fact]
    public async Task Full_return_cancels_without_sale()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 5m);
        harness.SeedOpenShift();
        var created = await harness.CreditInvoices().CreateAsync(
            "cashier", "عميل", null,
            [new CreditLineInput { ProductId = product.Id, Quantity = 1m }]);

        var returned = await harness.CreditInvoices().ReturnLineAsync(
            "cashier", created.InvoiceId!.Value, product.Id, 1m);

        Assert.True(returned.Success);
        var invoice = await harness.Db.CreditInvoices.SingleAsync();
        Assert.Equal(CreditInvoiceStatuses.Cancelled, invoice.Status);
        Assert.Empty(harness.Db.Orders);
        Assert.Equal(5m, (await harness.Db.Products.SingleAsync()).StockQuantity);
    }

    [Fact]
    public async Task Walk_in_customer_is_saved_to_the_book()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 5m);
        harness.SeedOpenShift();

        await harness.CreditInvoices().CreateAsync(
            "cashier", "سعيد", "0111",
            [new CreditLineInput { ProductId = product.Id, Quantity = 1m }]);

        var customer = Assert.Single(harness.Db.Customers);
        Assert.Equal("سعيد", customer.Name);
        Assert.Equal("0111", customer.Phone);
    }

    [Fact]
    public async Task Payment_over_remaining_is_rejected()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 5m);
        harness.SeedOpenShift();
        var created = await harness.CreditInvoices().CreateAsync(
            "cashier", "عميل", null,
            [new CreditLineInput { ProductId = product.Id, Quantity = 1m }]);

        var pay = await harness.CreditInvoices().RecordPaymentAsync("cashier", created.InvoiceId!.Value, 20m);

        Assert.False(pay.Success);
        Assert.Empty(harness.Db.CreditPayments);
        Assert.Empty(harness.Db.Orders);
    }
}
