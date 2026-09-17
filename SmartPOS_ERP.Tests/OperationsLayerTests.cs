using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Tests;

public class OperationsLayerTests
{
    [Fact]
    public async Task SaveOrder_applies_discount_tax_and_payment_method()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m, taxRate: 14m);

        var result = await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            DiscountAmount = 20m,
            PaymentMethod = PaymentMethods.Card,
            OrderDetails =
            [
                new OrderDetailViewModel { ProductId = product.Id, Quantity = 2m, UnitPrice = 1m }
            ]
        });

        Assert.IsType<OkObjectResult>(result);
        var order = await harness.Db.Orders.SingleAsync();
        Assert.Equal(20m, order.DiscountAmount);
        Assert.Equal(PaymentMethods.Card, order.PaymentMethod);
        Assert.Equal(11.20m, order.TaxAmount);
        Assert.Equal(91.20m, order.TotalAmount);
    }

    [Fact]
    public async Task SaveOrder_rejects_disabled_payment_method()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m);
        var settings = await harness.StoreSettings().GetAsync();
        settings.EnablePaymentCard = false;
        await harness.Db.SaveChangesAsync();

        var result = await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            PaymentMethod = PaymentMethods.Card,
            OrderDetails = [new OrderDetailViewModel { ProductId = product.Id, Quantity = 1m }]
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, await harness.Db.Orders.CountAsync());
    }

    [Fact]
    public async Task SaveOrder_omits_receipt_url_until_print_is_enabled()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m);

        var off = Assert.IsType<OkObjectResult>(await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails = [new OrderDetailViewModel { ProductId = product.Id, Quantity = 1m }]
        }));
        Assert.DoesNotContain("receiptUrl", System.Text.Json.JsonSerializer.Serialize(off.Value), StringComparison.OrdinalIgnoreCase);

        var settings = await harness.StoreSettings().GetAsync();
        settings.PrintReceiptAfterSale = true;
        await harness.Db.SaveChangesAsync();

        var on = Assert.IsType<OkObjectResult>(await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails = [new OrderDetailViewModel { ProductId = product.Id, Quantity = 1m }]
        }));
        var json = System.Text.Json.JsonSerializer.Serialize(on.Value);
        Assert.Contains("receiptUrl", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/Order/Receipt/", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveOrder_rejects_discount_above_subtotal()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m);

        var result = await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            DiscountAmount = 101m,
            OrderDetails =
            [
                new OrderDetailViewModel { ProductId = product.Id, Quantity = 2m }
            ]
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, await harness.Db.Orders.CountAsync());
    }

    [Fact]
    public async Task AdjustStock_sets_quantity_with_reason()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 8m);

        var result = await harness.ProductsController().AdjustStock(new AdjustStockRequest
        {
            ProductId = product.Id,
            NewQuantity = 5m,
            Reason = StockAdjustmentReasons.Damage,
            Notes = "كسر"
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(5m, (await harness.Db.Products.SingleAsync()).StockQuantity);
        var ledger = await harness.Db.StockLedgers.SingleAsync();
        Assert.Equal(StockMovementTypes.Adjustment, ledger.MovementType);
        Assert.Equal(-3m, ledger.QuantityChange);
        Assert.Contains("تلف", ledger.Reason);
    }

    [Fact]
    public async Task VoidPurchase_reverses_stock_and_excludes_from_totals()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 5m, stock: 0m);
        var supplier = new Supplier { Name = "مورد", Invoices = [], Payments = [] };
        harness.Db.Suppliers.Add(supplier);
        await harness.Db.SaveChangesAsync();
        harness.SeedOpenShift();

        var save = await harness.PurchasesController().SavePurchase(new PurchaseViewModel
        {
            SupplierId = supplier.Id,
            PurchaseDate = DateTime.Now,
            Items =
            [
                new PurchaseItemViewModel
                {
                    ProductId = product.Id,
                    PackageQuantity = 2m,
                    UnitsPerPackage = 5m,
                    PackageCost = 20m
                }
            ]
        });
        Assert.IsType<OkObjectResult>(save);
        Assert.Equal(10m, (await harness.Db.Products.SingleAsync()).StockQuantity);

        var invoice = await harness.Db.PurchaseInvoices.SingleAsync();
        var voidResult = await harness.PurchasesController().VoidPurchase(invoice.Id);
        Assert.IsType<RedirectToActionResult>(voidResult);

        var voided = await harness.Db.PurchaseInvoices.SingleAsync();
        Assert.True(voided.IsVoided);
        Assert.Equal(0m, (await harness.Db.Products.SingleAsync()).StockQuantity);

        var report = await new PurchaseInvoiceReportService(harness.Db)
            .BuildAsync("month", null, DateTime.Now.Month, DateTime.Now.Year, DateTime.Now);
        Assert.Equal(0m, report.TotalAmount);
        Assert.Equal(0, report.InvoiceCount);
        Assert.True(Assert.Single(report.Items).IsVoided);
    }

    [Fact]
    public async Task Dashboard_cash_counts_only_cash_sales()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 20m);
        var shift = harness.SeedOpenShift();

        await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            PaymentMethod = PaymentMethods.Cash,
            OrderDetails = [new OrderDetailViewModel { ProductId = product.Id, Quantity = 1m }]
        });
        await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            PaymentMethod = PaymentMethods.Card,
            OrderDetails = [new OrderDetailViewModel { ProductId = product.Id, Quantity = 1m }]
        });

        var model = await harness.Dashboard().BuildAsync(DateTime.Now);
        Assert.Equal(shift.OpeningCash + 50m, model.CashBalance);
    }

    [Fact]
    public async Task ImportCsv_creates_products_and_skips_duplicate_barcode()
    {
        await using var harness = new PosHarness();
        harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 1m);
        var existing = await harness.Db.Products.SingleAsync();
        existing.Barcode = "111";
        await harness.Db.SaveChangesAsync();

        var csv = "Name,Barcode,CostPrice,SalePrice,StockQuantity,Unit,Category,ExpiryDate\n" +
                  "شاي,111,2,5,3,Piece,مشروبات,\n" +
                  "سكر,222,4,8,10,Piece,معلبات,2026-12-01\n";
        var file = CsvFile(csv);

        var result = await harness.ProductsController().Import(file);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var products = await harness.Db.Products.OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(2, products.Count);
        Assert.Equal("سكر", products[1].Name);
        Assert.Equal("معلبات", products[1].Category);
        Assert.Equal(new DateTime(2026, 12, 1), products[1].ExpiryDate);
        Assert.Equal(10m, products[1].StockQuantity);
    }

    [Fact]
    public async Task HoldSale_roundtrip_restores_payload()
    {
        await using var harness = new PosHarness();
        var hold = await harness.ProductsController().HoldSale(new HoldSaleRequest
        {
            PayloadJson = """{"cart":[{"id":"1","qty":2}],"discountAmount":5,"paymentMethod":"Card"}""",
            LineCount = 1,
            TotalAmount = 45m
        });
        Assert.IsType<OkObjectResult>(hold);
        Assert.Equal(1, await harness.Db.HeldSales.CountAsync());

        var list = await harness.ProductsController().HeldSales();
        var json = Assert.IsType<JsonResult>(list);
        Assert.NotNull(json.Value);

        var held = await harness.Db.HeldSales.SingleAsync();
        var restore = await harness.ProductsController().RestoreHeldSale(held.Id);
        var restored = Assert.IsType<JsonResult>(restore);
        Assert.NotNull(restored.Value);
        Assert.Equal(0, await harness.Db.HeldSales.CountAsync());
    }

    [Fact]
    public async Task EditSupplier_updates_name_and_phone()
    {
        await using var harness = new PosHarness();
        var supplier = new Supplier { Name = "قديم", Phone = "1", Invoices = [], Payments = [] };
        harness.Db.Suppliers.Add(supplier);
        await harness.Db.SaveChangesAsync();

        var result = await harness.PurchasesController().EditSupplier(supplier.Id, new Supplier
        {
            Id = supplier.Id,
            Name = "جديد",
            Phone = "010",
            Address = "شارع 1",
            Notes = "ملاحظة"
        });

        Assert.IsType<RedirectToActionResult>(result);
        var updated = await harness.Db.Suppliers.SingleAsync();
        Assert.Equal("جديد", updated.Name);
        Assert.Equal("010", updated.Phone);
        Assert.Equal("شارع 1", updated.Address);
    }

    [Fact]
    public async Task CreateCustomer_adds_directory_row()
    {
        await using var harness = new PosHarness();
        var result = await harness.CustomersController().Create(new Customer
        {
            Name = "عميل",
            Phone = "012",
            Notes = "دائم"
        });
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("عميل", (await harness.Db.Customers.SingleAsync()).Name);
    }

    private static Microsoft.AspNetCore.Http.FormFile CsvFile(string csv)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        var stream = new MemoryStream(bytes);
        return new Microsoft.AspNetCore.Http.FormFile(stream, 0, bytes.Length, "file", "products.csv")
        {
            Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
            ContentType = "text/csv"
        };
    }
}
