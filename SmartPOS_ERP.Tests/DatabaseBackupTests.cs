using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Services;
using SmartPOS_ERP.Security;

namespace SmartPOS_ERP.Tests;

public class DatabaseBackupTests
{
    [Fact]
    public async Task Export_then_import_restores_rows_and_drops_later_data()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 8m);
        product.Name = "لبن";
        var shift = harness.SeedOpenShift("manager");
        harness.Db.Orders.Add(new Order
        {
            OrderDate = new DateTime(2026, 1, 2, 10, 0, 0),
            TotalAmount = 10m,
            TaxAmount = 0m,
            ShiftId = shift.Id,
            OrderDetails =
            [
                new OrderDetail
                {
                    ProductId = product.Id,
                    Quantity = 1m,
                    UnitPrice = 10m,
                    UnitCost = 4m,
                    TaxRate = 0m
                }
            ]
        });
        harness.Db.Users.Add(new User
        {
            Username = "cashier2",
            DisplayName = "كاشير",
            Password = "hashed",
            Role = "Cashier",
            IsActive = true
        });
        await harness.StoreSettings().SaveAsync(new StoreSettings
        {
            StoreName = "محل النور",
            InvoiceFooter = "شكراً",
            TaxEnabled = true,
            DefaultTaxRate = 14m
        }, null, false);
        await harness.Db.SaveChangesAsync();

        var zip = await harness.Backup().ExportAsync();

        harness.SeedProduct(salePrice: 3m, costPrice: 1m, stock: 1m);
        Assert.Equal(2, await harness.Db.Products.CountAsync());

        var error = await harness.Backup().ImportAsync(new MemoryStream(zip));
        Assert.Null(error);

        Assert.Equal(1, await harness.Db.Products.CountAsync());
        Assert.Equal("لبن", (await harness.Db.Products.SingleAsync()).Name);
        Assert.Equal(1, await harness.Db.Orders.CountAsync());
        Assert.Equal(1, await harness.Db.OrderDetails.CountAsync());
        Assert.Contains(await harness.Db.Users.ToListAsync(), u => u.Username == "cashier2");
        Assert.Equal("محل النور", (await harness.StoreSettings().GetAsync()).StoreName);
    }

    [Fact]
    public async Task Import_rejects_corrupt_or_wrong_version_without_changing_data()
    {
        await using var harness = new PosHarness();
        harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 5m);

        var corrupt = await harness.Backup().ImportAsync(new MemoryStream("not-a-zip"u8.ToArray()));
        Assert.Equal("ملف النسخة الاحتياطية غير صالح.", corrupt);

        await using var wrongVersion = new MemoryStream();
        using (var zip = new ZipArchive(wrongVersion, ZipArchiveMode.Create, leaveOpen: true))
        {
            await using (var manifest = zip.CreateEntry("manifest.json").Open())
            {
                await manifest.WriteAsync("""{"FormatVersion":2,"App":"Sama_POS"}"""u8.ToArray());
            }

            await using (var data = zip.CreateEntry("data.json").Open())
            {
                await data.WriteAsync("{}"u8.ToArray());
            }
        }

        wrongVersion.Position = 0;
        var versionError = await harness.Backup().ImportAsync(wrongVersion);
        Assert.Equal("إصدار ملف النسخة غير مدعوم.", versionError);
        Assert.Equal(1, await harness.Db.Products.CountAsync());
    }

    [Fact]
    public async Task Reset_keeps_current_admin_and_wipes_everything_else()
    {
        await using var harness = new PosHarness();
        harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 5m);
        var shift = harness.SeedOpenShift("manager");
        harness.Db.Users.Add(new User
        {
            Username = "cashier2",
            DisplayName = "كاشير",
            Password = "hashed",
            Role = "Cashier",
            IsActive = true
        });
        harness.Db.Expenses.Add(new Expense
        {
            Description = "إيجار",
            Amount = 100m,
            ExpenseDate = DateTime.Today,
            Category = "إيجار"
        });
        await harness.StoreSettings().SaveAsync(new StoreSettings
        {
            StoreName = "محل النور",
            Phone = "0100",
            InvoiceFooter = "شكراً",
            TaxEnabled = false,
            DefaultTaxRate = 14m
        }, null, false);
        var settings = await harness.StoreSettings().GetAsync();
        settings.FactoryResetPinHash = "hash";
        await harness.Db.SaveChangesAsync();

        var error = await harness.Backup().ResetAsync("manager");
        Assert.Null(error);

        Assert.Empty(harness.Db.Products);
        Assert.Empty(harness.Db.Orders);
        Assert.Empty(harness.Db.Expenses);
        Assert.Empty(harness.Db.Shifts);
        var remaining = harness.Db.Users.Select(u => u.Username).ToList();
        Assert.Contains("manager", remaining);
        Assert.Contains(OwnerAccount.Username, remaining);
        Assert.DoesNotContain("cashier2", remaining);
        Assert.Equal("Admin", harness.Db.Users.Single(u => u.Username == "manager").Role);

        var restored = await harness.StoreSettings().GetAsync();
        Assert.Equal("Sama_POS", restored.StoreName);
        Assert.Equal("شكراً لتعاملكم معنا — Sama_POS", restored.InvoiceFooter);
        Assert.True(restored.TaxEnabled);
        Assert.Equal(0m, restored.DefaultTaxRate);
        Assert.Null(restored.FactoryResetPinHash);
        Assert.Null(restored.Phone);
        Assert.Null(restored.LogoPath);
    }

    [Fact]
    public async Task Reset_fails_when_current_admin_is_missing()
    {
        await using var harness = new PosHarness();
        var error = await harness.Backup().ResetAsync("ghost");
        Assert.Equal("تعذر العثور على حساب المدير الحالي.", error);
    }
}
