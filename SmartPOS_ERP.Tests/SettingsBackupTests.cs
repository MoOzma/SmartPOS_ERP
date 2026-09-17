using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Security;

namespace SmartPOS_ERP.Tests;

public class SettingsBackupTests
{
    [Fact]
    public async Task SetResetPin_saves_four_digit_pin()
    {
        await using var harness = new PosHarness();
        var result = Assert.IsType<RedirectToActionResult>(
            await harness.SettingsController().SetResetPin(null, "2468", "2468"));

        Assert.Equal("Index", result.ActionName);
        var settings = await harness.StoreSettings().GetAsync();
        Assert.True(ResetPinRules.Matches("2468", settings.FactoryResetPinHash));
    }

    [Fact]
    public async Task FactoryReset_requires_saved_and_matching_pin()
    {
        await using var harness = new PosHarness();
        harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 5m);
        harness.SeedOpenShift();

        var missing = Assert.IsType<RedirectToActionResult>(
            await harness.SettingsController().FactoryReset("2468", confirm: true));
        Assert.Equal("Index", missing.ActionName);
        Assert.Equal(1, await harness.Db.Products.CountAsync());

        await harness.SettingsController().SetResetPin(null, "2468", "2468");

        var wrong = Assert.IsType<RedirectToActionResult>(
            await harness.SettingsController().FactoryReset("0000", confirm: true));
        Assert.Equal(1, await harness.Db.Products.CountAsync());

        var unconfirmed = Assert.IsType<RedirectToActionResult>(
            await harness.SettingsController().FactoryReset("2468", confirm: false));
        Assert.Equal(1, await harness.Db.Products.CountAsync());

        Assert.IsType<RedirectToActionResult>(
            await harness.SettingsController().FactoryReset("2468", confirm: true));
        Assert.Empty(harness.Db.Products);
        var remaining = harness.Db.Users.Select(u => u.Username).ToList();
        Assert.Contains("cashier", remaining);
        Assert.Contains(OwnerAccount.Username, remaining);
    }

    [Fact]
    public async Task ExportBackup_returns_zip_file()
    {
        await using var harness = new PosHarness();
        var result = Assert.IsType<FileContentResult>(await harness.SettingsController().ExportBackup());

        Assert.Equal("application/zip", result.ContentType);
        Assert.StartsWith("sama-pos-backup-", result.FileDownloadName);
        Assert.EndsWith(".zip", result.FileDownloadName);
        Assert.True(result.FileContents.Length > 20);
    }

    [Fact]
    public async Task ImportBackup_requires_pin_and_restores_file()
    {
        await using var harness = new PosHarness();
        var original = harness.SeedProduct(salePrice: 10m, costPrice: 4m, stock: 5m);
        original.Name = "لبن";
        await harness.Db.SaveChangesAsync();
        await harness.SettingsController().SetResetPin(null, "2468", "2468");
        var zip = await harness.Backup().ExportAsync();

        harness.SeedProduct(salePrice: 3m, costPrice: 1m, stock: 1m);
        var file = new FormFile(new MemoryStream(zip), 0, zip.Length, "backupFile", "backup.zip");

        var missingConfirm = Assert.IsType<RedirectToActionResult>(
            await harness.SettingsController().ImportBackup(file, "2468", confirm: false));
        Assert.Equal(2, await harness.Db.Products.CountAsync());

        var wrongPin = Assert.IsType<RedirectToActionResult>(
            await harness.SettingsController().ImportBackup(file, "0000", confirm: true));
        Assert.Equal(2, await harness.Db.Products.CountAsync());

        Assert.IsType<RedirectToActionResult>(
            await harness.SettingsController().ImportBackup(file, "2468", confirm: true));
        Assert.Equal(1, await harness.Db.Products.CountAsync());
        Assert.Equal("لبن", (await harness.Db.Products.SingleAsync()).Name);
    }
}
