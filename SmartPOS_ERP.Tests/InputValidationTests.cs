using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Tests;

public class InputValidationTests
{
    [Fact]
    public async Task Create_product_rejects_zero_sale_price()
    {
        await using var harness = new PosHarness();
        var result = await harness.ProductsController().Create(new Product
        {
            Name = "صنف بسعر صفر",
            Unit = "Piece",
            SalePrice = 0m,
            CostPrice = 5m,
            TrackInventory = true
        });

        Assert.IsType<ViewResult>(result);
        Assert.Empty(await harness.Db.Products.ToListAsync());
    }

    [Fact]
    public async Task Create_product_rejects_negative_cost_price()
    {
        await using var harness = new PosHarness();
        var result = await harness.ProductsController().Create(new Product
        {
            Name = "صنف بتكلفة سالبة",
            Unit = "Piece",
            SalePrice = 10m,
            CostPrice = -1m,
            TrackInventory = true
        });

        Assert.IsType<ViewResult>(result);
        Assert.Empty(await harness.Db.Products.ToListAsync());
    }

    [Fact]
    public async Task Create_product_rejects_negative_stock()
    {
        await using var harness = new PosHarness();
        var result = await harness.ProductsController().Create(new Product
        {
            Name = "صنف بمخزون سالب",
            Unit = "Piece",
            SalePrice = 10m,
            CostPrice = 5m,
            StockQuantity = -3m,
            TrackInventory = true
        });

        Assert.IsType<ViewResult>(result);
        Assert.Empty(await harness.Db.Products.ToListAsync());
    }

    [Fact]
    public async Task Create_product_rejects_sale_price_equal_to_cost()
    {
        await using var harness = new PosHarness();
        var result = await harness.ProductsController().Create(new Product
        {
            Name = "كيلو بنفس السعر",
            Unit = "Kilo",
            SalePrice = 80m,
            CostPrice = 80m,
            TrackInventory = true
        });

        Assert.IsType<ViewResult>(result);
        Assert.Empty(await harness.Db.Products.ToListAsync());
    }

    [Fact]
    public async Task Create_product_rejects_sale_price_below_cost()
    {
        await using var harness = new PosHarness();
        var result = await harness.ProductsController().Create(new Product
        {
            Name = "كيلو بخسارة",
            Unit = "Kilo",
            SalePrice = 70m,
            CostPrice = 80m,
            TrackInventory = true
        });

        Assert.IsType<ViewResult>(result);
        Assert.Empty(await harness.Db.Products.ToListAsync());
    }

    [Fact]
    public void Product_attributes_reject_zero_prices()
    {
        var product = new Product
        {
            Name = "خطأ",
            Unit = "Piece",
            SalePrice = 0m,
            CostPrice = 0m,
            StockQuantity = -1m,
            TaxRate = 140m
        };
        var results = new List<ValidationResult>();

        var ok = Validator.TryValidateObject(product, new ValidationContext(product), results, true);

        Assert.False(ok);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Product.SalePrice)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Product.CostPrice)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Product.StockQuantity)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Product.TaxRate)));
    }

    [Fact]
    public void Product_rules_reject_sale_not_above_cost()
    {
        var product = new Product
        {
            Name = "كيلو",
            Barcode = "8001",
            Unit = "Kilo",
            SalePrice = 80m,
            CostPrice = 80m
        };

        var errors = InputRules.ProductErrors(product).ToList();

        Assert.Contains(errors, e => e.Key == nameof(Product.SalePrice) && e.Message == InputRules.SaleMustExceedCost);
        Assert.Single(errors, e => e.Message == InputRules.SaleMustExceedCost);
    }

    [Fact]
    public async Task SavePurchase_rejects_zero_package_cost()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(10m, 4m, 5m);
        harness.SeedOpenShift();
        var supplier = new Supplier { Name = "مورد", Invoices = [], Payments = [] };
        harness.Db.Suppliers.Add(supplier);
        await harness.Db.SaveChangesAsync();

        var result = await harness.PurchasesController().SavePurchase(new PurchaseViewModel
        {
            SupplierId = supplier.Id,
            Items =
            [
                new PurchaseItemViewModel
                {
                    ProductId = product.Id,
                    PackageQuantity = 2m,
                    UnitsPerPackage = 1m,
                    PackageCost = 0m
                }
            ]
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await harness.Db.PurchaseInvoices.ToListAsync());
        Assert.Equal(4m, (await harness.Db.Products.SingleAsync()).CostPrice);
    }

    [Fact]
    public async Task SavePurchase_rejects_zero_new_sale_price()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(10m, 4m, 5m);
        harness.SeedOpenShift();
        var supplier = new Supplier { Name = "مورد", Invoices = [], Payments = [] };
        harness.Db.Suppliers.Add(supplier);
        await harness.Db.SaveChangesAsync();

        var result = await harness.PurchasesController().SavePurchase(new PurchaseViewModel
        {
            SupplierId = supplier.Id,
            Items =
            [
                new PurchaseItemViewModel
                {
                    ProductId = product.Id,
                    PackageQuantity = 1m,
                    UnitsPerPackage = 1m,
                    PackageCost = 4m,
                    NewSalePrice = 0m
                }
            ]
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(10m, (await harness.Db.Products.SingleAsync()).SalePrice);
    }

    [Fact]
    public async Task SavePurchase_rejects_sale_not_above_unit_cost()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(10m, 4m, 5m);
        harness.SeedOpenShift();
        var supplier = new Supplier { Name = "مورد", Invoices = [], Payments = [] };
        harness.Db.Suppliers.Add(supplier);
        await harness.Db.SaveChangesAsync();

        var result = await harness.PurchasesController().SavePurchase(new PurchaseViewModel
        {
            SupplierId = supplier.Id,
            Items =
            [
                new PurchaseItemViewModel
                {
                    ProductId = product.Id,
                    PackageQuantity = 1m,
                    UnitsPerPackage = 1m,
                    PackageCost = 8m,
                    NewSalePrice = 8m
                }
            ]
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(10m, (await harness.Db.Products.SingleAsync()).SalePrice);
        Assert.Empty(await harness.Db.PurchaseInvoices.ToListAsync());
    }

    [Fact]
    public async Task SaveOrder_rejects_zero_quantity()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(10m, 4m, 5m);

        var result = await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails = [new OrderDetailViewModel { ProductId = product.Id, Quantity = 0m }]
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await harness.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task SaveOrder_rejects_negative_discount()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(10m, 4m, 5m);

        var result = await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            DiscountAmount = -5m,
            OrderDetails = [new OrderDetailViewModel { ProductId = product.Id, Quantity = 1m }]
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await harness.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task SaveOrder_rejects_product_with_zero_sale_price()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(10m, 4m, 5m);
        product.SalePrice = 0m;
        await harness.Db.SaveChangesAsync();

        var result = await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails = [new OrderDetailViewModel { ProductId = product.Id, Quantity = 1m }]
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await harness.Db.Orders.ToListAsync());
        Assert.Equal(5m, (await harness.Db.Products.SingleAsync()).StockQuantity);
    }

    [Fact]
    public async Task Expense_rejects_zero_amount()
    {
        await using var harness = new PosHarness();
        harness.SeedOpenShift();
        var result = await harness.ExpensesController().Create(new Expense
        {
            Category = "نثريات",
            Description = "خطأ",
            Amount = 0m,
            ExpenseDate = DateTime.Today,
            FromCash = false
        });

        Assert.IsType<ViewResult>(result);
        Assert.Empty(await harness.Db.Expenses.ToListAsync());
    }

    [Fact]
    public void Csv_import_skips_zero_or_negative_prices()
    {
        var products = ProductCsvImporter.Parse(
            "Name,Barcode,CostPrice,SalePrice,StockQuantity,Unit\nBad,,0,10,1,Piece\nNeg,,5,-2,1,Piece\nLoss,,80,70,1,Kilo\nGood,123,5,10,1,Piece",
            [],
            out var skippedDuplicates,
            out var skippedInvalid);

        Assert.Single(products);
        Assert.Equal("Good", products[0].Name);
        Assert.Equal(0, skippedDuplicates);
        Assert.Equal(3, skippedInvalid);
    }

    [Fact]
    public async Task Credit_invoice_rejects_zero_sale_price()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(10m, 4m, 5m);
        product.SalePrice = 0m;
        await harness.Db.SaveChangesAsync();
        harness.SeedOpenShift();

        var result = await harness.CreditInvoices().CreateAsync(
            "cashier",
            "عميل",
            null,
            [new CreditLineInput { ProductId = product.Id, Quantity = 1m }]);

        Assert.False(result.Success);
        Assert.Empty(await harness.Db.CreditInvoices.ToListAsync());
    }
}
