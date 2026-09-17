using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Tests;

public class ProductCreateTests
{
    [Fact]
    public async Task Create_post_saves_product_and_opening_stock()
    {
        await using var harness = new PosHarness();
        var result = await harness.ProductsController().Create(new Product
        {
            Name = "لب أسود",
            Barcode = "1001",
            Unit = "Piece",
            SalePrice = 10m,
            CostPrice = 6m,
            StockQuantity = 4m,
            TrackInventory = true,
            CartonCount = 1m,
            CartonPrice = 24m,
            PiecesPerCarton = 4m
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SmartPOS_ERP.Controllers.ProductsController.Index), redirect.ActionName);

        var product = Assert.Single(await harness.Db.Products.ToListAsync());
        Assert.Equal("لب أسود", product.Name);
        Assert.Equal(4m, product.StockQuantity);
        Assert.NotEmpty(product.RowVersion);
        Assert.Equal(1, await harness.Db.StockLedgers.CountAsync());
    }

    [Fact]
    public async Task Create_piece_from_carton_uses_unit_cost_for_profit()
    {
        await using var harness = new PosHarness();
        var result = await harness.ProductsController().Create(new Product
        {
            Name = "بسكويت",
            Barcode = "1002",
            Unit = "Piece",
            SalePrice = 12m,
            CostPrice = 10m,
            TrackInventory = true,
            CartonCount = 2m,
            CartonPrice = 100m,
            PiecesPerCarton = 10m,
            StockQuantity = 20m
        });

        Assert.IsType<RedirectToActionResult>(result);
        var product = Assert.Single(await harness.Db.Products.ToListAsync());
        Assert.Equal(10m, product.CostPrice);
        Assert.Equal(20m, product.StockQuantity);
        Assert.Equal(12m, product.SalePrice);
    }

    [Fact]
    public async Task Create_piece_rejects_cost_below_carton_unit_price()
    {
        await using var harness = new PosHarness();
        var result = await harness.ProductsController().Create(new Product
        {
            Name = "بسكويت",
            Barcode = "1003",
            Unit = "Piece",
            SalePrice = 12m,
            CostPrice = 9m,
            TrackInventory = true,
            CartonCount = 2m,
            CartonPrice = 100m,
            PiecesPerCarton = 10m,
            StockQuantity = 20m
        });

        Assert.IsType<ViewResult>(result);
        Assert.Empty(await harness.Db.Products.ToListAsync());
    }

    [Fact]
    public async Task Create_rejects_duplicate_name()
    {
        await using var harness = new PosHarness();
        await harness.ProductsController().Create(new Product
        {
            Name = "حمص",
            Barcode = "2001",
            Unit = "Kilo",
            SalePrice = 40m,
            CostPrice = 20m
        });

        var result = await harness.ProductsController().Create(new Product
        {
            Name = "حمص",
            Barcode = "2002",
            Unit = "Kilo",
            SalePrice = 45m,
            CostPrice = 22m
        });

        Assert.IsType<ViewResult>(result);
        Assert.Single(await harness.Db.Products.ToListAsync());
    }

    [Fact]
    public async Task Create_rejects_duplicate_barcode()
    {
        await using var harness = new PosHarness();
        await harness.ProductsController().Create(new Product
        {
            Name = "حمص",
            Barcode = "2001",
            Unit = "Kilo",
            SalePrice = 40m,
            CostPrice = 20m
        });

        var result = await harness.ProductsController().Create(new Product
        {
            Name = "فول",
            Barcode = "2001",
            Unit = "Kilo",
            SalePrice = 25m,
            CostPrice = 12m
        });

        Assert.IsType<ViewResult>(result);
        Assert.Single(await harness.Db.Products.ToListAsync());
    }

    [Fact]
    public async Task Create_allows_missing_barcode()
    {
        await using var harness = new PosHarness();
        var result = await harness.ProductsController().Create(new Product
        {
            Name = "بدون باركود",
            Unit = "Kilo",
            SalePrice = 40m,
            CostPrice = 20m
        });

        Assert.IsType<RedirectToActionResult>(result);
        var product = Assert.Single(await harness.Db.Products.ToListAsync());
        Assert.Null(product.Barcode);
    }

    [Fact]
    public async Task Create_sale_at_or_below_cost_shows_warning_once()
    {
        await using var harness = new PosHarness();
        var controller = harness.ProductsController();
        var result = await controller.Create(new Product
        {
            Name = "أرز",
            Barcode = "3001",
            Unit = "Kilo",
            SalePrice = 20m,
            CostPrice = 20m
        });

        Assert.IsType<ViewResult>(result);
        var messages = controller.ModelState
            .SelectMany(e => e.Value!.Errors)
            .Select(e => e.ErrorMessage)
            .Where(m => m == InputRules.SaleMustExceedCost)
            .ToList();
        Assert.Single(messages);
        Assert.Empty(await harness.Db.Products.ToListAsync());
    }

    [Fact]
    public void Create_form_does_not_require_row_version()
    {
        var product = new Product
        {
            Name = "حمص",
            Unit = "Kilo",
            SalePrice = 40m,
            CostPrice = 20m
        };
        var results = new List<ValidationResult>();

        var ok = Validator.TryValidateObject(product, new ValidationContext(product), results, true);

        Assert.True(ok);
        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(Product.RowVersion)));
    }
}
