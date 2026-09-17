using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartPOS_ERP.Controllers;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Tests;

public class CloseTheLoopTests
{
    [Fact]
    public async Task Cash_expense_and_supplier_payment_reduce_shift_drawer()
    {
        await using var harness = new PosHarness();
        var shift = harness.SeedOpenShift();
        shift.OpeningCash = 200m;
        await harness.Db.SaveChangesAsync();

        var expense = await harness.ExpensesController().Create(new Expense
        {
            Category = "نثريات",
            Description = "أكياس",
            Amount = 30m,
            ExpenseDate = DateTime.Today,
            FromCash = true
        });
        Assert.IsType<RedirectToActionResult>(expense);

        var supplier = new Supplier { Name = "مورد", Invoices = [], Payments = [] };
        harness.Db.Suppliers.Add(supplier);
        await harness.Db.SaveChangesAsync();
        var pay = await harness.PurchasesController().PaySupplier(supplier.Id, 50m, DateTime.Today, "كاش", fromCash: true);
        Assert.IsType<RedirectToActionResult>(pay);

        var expected = await new ShiftCashService(harness.Db).ExpectedAsync(shift.Id, shift.OpeningCash);
        Assert.Equal(120m, expected);
        Assert.Equal(120m, (await harness.Dashboard().BuildAsync(DateTime.Now)).CashBalance);
    }

    [Fact]
    public async Task Bank_expense_does_not_reduce_drawer()
    {
        await using var harness = new PosHarness();
        var shift = harness.SeedOpenShift();
        shift.OpeningCash = 100m;
        await harness.Db.SaveChangesAsync();

        await harness.ExpensesController().Create(new Expense
        {
            Category = "إيجار",
            Description = "تحويل",
            Amount = 40m,
            ExpenseDate = DateTime.Today,
            FromCash = false
        });

        Assert.Equal(100m, await new ShiftCashService(harness.Db).ExpectedAsync(shift.Id, shift.OpeningCash));
    }

    [Fact]
    public async Task Cash_expense_without_shift_is_rejected()
    {
        await using var harness = new PosHarness();
        var result = await harness.ExpensesController().Create(new Expense
        {
            Category = "نثريات",
            Description = "بدون وردية",
            Amount = 10m,
            ExpenseDate = DateTime.Today,
            FromCash = true
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Open", redirect.ActionName);
        Assert.Empty(harness.Db.Expenses);
    }

    [Fact]
    public async Task Purchase_return_reduces_stock_and_supplier_balance()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 0m);
        var user = harness.Db.Users.SingleOrDefault(u => u.Username == "cashier") ?? new User
        {
            Username = "cashier",
            DisplayName = "كاشير",
            Password = "hashed",
            Role = "Admin",
            IsActive = true
        };
        if (user.Id == 0)
        {
            harness.Db.Users.Add(user);
            await harness.Db.SaveChangesAsync();
        }

        var supplier = new Supplier { Name = "مورد", Invoices = [], Payments = [] };
        harness.Db.Suppliers.Add(supplier);
        await harness.Db.SaveChangesAsync();
        harness.Db.Shifts.Add(new Shift { UserId = user.Id, OpenedAt = DateTime.Now, OpeningCash = 0m });
        await harness.Db.SaveChangesAsync();

        await harness.PurchasesController().SavePurchase(new PurchaseViewModel
        {
            SupplierId = supplier.Id,
            Items =
            [
                new PurchaseItemViewModel { ProductId = product.Id, PackageQuantity = 10m, UnitsPerPackage = 1m, PackageCost = 4m }
            ]
        });

        var invoice = await harness.Db.PurchaseInvoices.Include(i => i.Details).SingleAsync();
        var detailId = invoice.Details.Single().Id;

        var result = await harness.PurchasesController().ReturnPurchase(invoice.Id, new PurchaseReturnForm
        {
            Notes = "تالف",
            Lines = [new PurchaseReturnLineForm { PurchaseDetailId = detailId, Quantity = 3m }]
        });
        Assert.IsType<RedirectToActionResult>(result);

        Assert.Equal(7m, (await harness.Db.Products.SingleAsync()).StockQuantity);
        var directory = await new SupplierDirectoryService(harness.Db).BuildAsync(null, null);
        Assert.Equal(28m, Assert.Single(directory.Items).Balance);
        Assert.Equal(28m, (await harness.Dashboard().BuildAsync(DateTime.Now)).SupplierPayables);
    }

    [Fact]
    public async Task Dashboard_shows_open_customer_receivables()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 8m);
        harness.SeedOpenShift();
        await harness.CreditInvoices().CreateAsync(
            "cashier",
            "عميل آجل",
            null,
            [new CreditLineInput { ProductId = product.Id, Quantity = 2m }]);

        var model = await harness.Dashboard().BuildAsync(DateTime.Now);
        Assert.Equal(20m, model.CustomerReceivables);
    }

    [Fact]
    public async Task Expiry_report_lists_expired_and_soon_not_later()
    {
        await using var harness = new PosHarness();
        var expired = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 1m);
        expired.Name = "منتهي";
        expired.ExpiryDate = DateTime.Today.AddDays(-1);
        var soon = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 1m);
        soon.Name = "قريب";
        soon.ExpiryDate = DateTime.Today.AddDays(3);
        var later = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 1m);
        later.Name = "بعيد";
        later.ExpiryDate = DateTime.Today.AddDays(30);
        await harness.Db.SaveChangesAsync();

        var reports = new ReportsController(new SalesProfitReportService(harness.Db), harness.Db);
        var result = await reports.ExpiryReport();
        var names = Assert.IsAssignableFrom<IEnumerable<Product>>(Assert.IsType<ViewResult>(result).Model)
            .Select(p => p.Name)
            .ToList();
        Assert.Contains("منتهي", names);
        Assert.Contains("قريب", names);
        Assert.DoesNotContain("بعيد", names);

        var dash = await harness.Dashboard().BuildAsync(DateTime.Now);
        Assert.Equal(2, dash.ExpiringCount);
    }
}
