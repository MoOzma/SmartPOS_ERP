using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Tests;

public class PurchaseShiftTests
{
    [Fact]
    public async Task Create_redirects_to_open_shift_when_none()
    {
        await using var harness = new PosHarness();
        var result = await harness.PurchasesController().Create();
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Open", redirect.ActionName);
        Assert.Equal("Shifts", redirect.ControllerName);
    }

    [Fact]
    public async Task SavePurchase_rejects_without_open_shift()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 0m);
        var supplier = new Supplier { Name = "مورد", Invoices = [], Payments = [] };
        harness.Db.Suppliers.Add(supplier);
        await harness.Db.SaveChangesAsync();

        var result = await harness.PurchasesController().SavePurchase(new PurchaseViewModel
        {
            SupplierId = supplier.Id,
            Items =
            [
                new PurchaseItemViewModel { ProductId = product.Id, PackageQuantity = 1m, UnitsPerPackage = 1m }
            ]
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(harness.Db.PurchaseInvoices);
    }

    [Fact]
    public async Task SavePurchase_succeeds_with_open_shift()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 0m);
        var user = new User
        {
            Username = "cashier",
            DisplayName = "كاشير",
            Password = "hashed",
            Role = "Admin",
            IsActive = true
        };
        var supplier = new Supplier { Name = "مورد", Invoices = [], Payments = [] };
        harness.Db.Users.Add(user);
        harness.Db.Suppliers.Add(supplier);
        await harness.Db.SaveChangesAsync();
        harness.Db.Shifts.Add(new Shift { UserId = user.Id, OpenedAt = DateTime.Now, OpeningCash = 0m });
        await harness.Db.SaveChangesAsync();

        var result = await harness.PurchasesController().SavePurchase(new PurchaseViewModel
        {
            SupplierId = supplier.Id,
            Items =
            [
                new PurchaseItemViewModel { ProductId = product.Id, PackageQuantity = 2m, UnitsPerPackage = 1m }
            ]
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(harness.Db.PurchaseInvoices);
        Assert.Equal(2m, (await harness.Db.Products.SingleAsync()).StockQuantity);
    }
}
