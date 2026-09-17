using SmartPOS_ERP.Models;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Tests;

public class StoreSettingsTests
{
    [Fact]
    public void EffectiveTaxRate_is_zero_when_tax_disabled()
    {
        var product = new Product { UseCustomTax = true, TaxRate = 14m };
        var settings = new StoreSettings { TaxEnabled = false, DefaultTaxRate = 14m };

        Assert.Equal(0m, StoreSettingsService.EffectiveTaxRate(product, settings));
    }

    [Fact]
    public void EffectiveTaxRate_uses_custom_zero_to_exempt_product()
    {
        var product = new Product { UseCustomTax = true, TaxRate = 0m };
        var settings = new StoreSettings { TaxEnabled = true, DefaultTaxRate = 14m };

        Assert.Equal(0m, StoreSettingsService.EffectiveTaxRate(product, settings));
    }

    [Fact]
    public void EffectiveTaxRate_uses_custom_rate_when_set()
    {
        var product = new Product { UseCustomTax = true, TaxRate = 14m };
        var settings = new StoreSettings { TaxEnabled = true, DefaultTaxRate = 10m };

        Assert.Equal(14m, StoreSettingsService.EffectiveTaxRate(product, settings));
    }

    [Fact]
    public void EffectiveTaxRate_uses_default_when_product_has_no_override()
    {
        var product = new Product { UseCustomTax = false, TaxRate = 0m };
        var settings = new StoreSettings { TaxEnabled = true, DefaultTaxRate = 14m };

        Assert.Equal(14m, StoreSettingsService.EffectiveTaxRate(product, settings));
    }

    [Fact]
    public async Task GetAsync_creates_default_singleton_row()
    {
        await using var harness = new PosHarness();
        var settings = await harness.StoreSettings().GetAsync();

        Assert.Equal(1, settings.Id);
        Assert.Equal("Sama_POS", settings.StoreName);
        Assert.Equal("شكراً لتعاملكم معنا — Sama_POS", settings.InvoiceFooter);
        Assert.True(settings.TaxEnabled);
        Assert.Equal(0m, settings.DefaultTaxRate);
        Assert.False(settings.PrintReceiptAfterSale);
        Assert.True(settings.EnablePaymentCash);
        Assert.True(settings.EnablePaymentCard);
        Assert.True(settings.EnablePaymentTransfer);
        Assert.Equal(1, harness.Db.StoreSettings.Count());
    }

    [Fact]
    public async Task SaveAsync_updates_print_and_payment_methods()
    {
        await using var harness = new PosHarness();
        var error = await harness.StoreSettings().SaveAsync(new StoreSettings
        {
            StoreName = "محل النور",
            InvoiceFooter = "شكراً",
            PrintReceiptAfterSale = true,
            EnablePaymentCash = true,
            EnablePaymentCard = false,
            EnablePaymentTransfer = true
        }, null, false);

        Assert.Null(error);
        var saved = await harness.StoreSettings().GetAsync();
        Assert.True(saved.PrintReceiptAfterSale);
        Assert.True(saved.EnablePaymentCash);
        Assert.False(saved.EnablePaymentCard);
        Assert.True(saved.EnablePaymentTransfer);
        Assert.Equal(new[] { PaymentMethods.Cash, PaymentMethods.Transfer }, PaymentMethods.Enabled(saved));
    }

    [Fact]
    public async Task SaveAsync_keeps_cash_when_no_payment_method_selected()
    {
        await using var harness = new PosHarness();
        await harness.StoreSettings().SaveAsync(new StoreSettings
        {
            StoreName = "محل النور",
            InvoiceFooter = "شكراً",
            EnablePaymentCash = false,
            EnablePaymentCard = false,
            EnablePaymentTransfer = false
        }, null, false);

        var saved = await harness.StoreSettings().GetAsync();
        Assert.True(saved.EnablePaymentCash);
        Assert.False(saved.EnablePaymentCard);
        Assert.False(saved.EnablePaymentTransfer);
    }

    [Fact]
    public async Task SaveAsync_updates_store_identity_and_tax()
    {
        await using var harness = new PosHarness();
        var error = await harness.StoreSettings().SaveAsync(new StoreSettings
        {
            StoreName = " محل النور ",
            Phone = "0100",
            Address = "القاهرة",
            InvoiceFooter = "شكراً لزيارتكم",
            TaxEnabled = true,
            DefaultTaxRate = 14m
        }, null, false);

        Assert.Null(error);
        var saved = await harness.StoreSettings().GetAsync();
        Assert.Equal("محل النور", saved.StoreName);
        Assert.Equal("0100", saved.Phone);
        Assert.Equal("القاهرة", saved.Address);
        Assert.Equal("شكراً لزيارتكم", saved.InvoiceFooter);
        Assert.True(saved.TaxEnabled);
        Assert.Equal(14m, saved.DefaultTaxRate);
    }
}
