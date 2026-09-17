using SmartPOS_ERP.Models;
using SmartPOS_ERP.Security;

namespace SmartPOS_ERP.Tests;

public class ResetPinTests
{
    [Theory]
    [InlineData("1234")]
    [InlineData("0000")]
    [InlineData("9876")]
    public void TryValidate_accepts_four_digits(string pin)
    {
        Assert.True(ResetPinRules.TryValidate(pin, out var error));
        Assert.Equal(string.Empty, error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("abcd")]
    [InlineData("12ab")]
    [InlineData("12 4")]
    public void TryValidate_rejects_non_four_digit_pin(string? pin)
    {
        Assert.False(ResetPinRules.TryValidate(pin, out var error));
        Assert.Equal("الرقم السري يجب أن يكون 4 أرقام.", error);
    }

    [Fact]
    public void Apply_sets_pin_the_first_time_without_current()
    {
        var settings = new StoreSettings();

        var error = ResetPinRules.Apply(settings, currentPin: null, newPin: "2468", confirm: "2468");

        Assert.Null(error);
        Assert.False(string.IsNullOrEmpty(settings.FactoryResetPinHash));
        Assert.True(ResetPinRules.Matches("2468", settings.FactoryResetPinHash));
        Assert.False(ResetPinRules.Matches("0000", settings.FactoryResetPinHash));
    }

    [Fact]
    public void Apply_requires_matching_confirmation()
    {
        var settings = new StoreSettings();

        var error = ResetPinRules.Apply(settings, null, "2468", "2469");

        Assert.Equal("تأكيد الرقم غير مطابق.", error);
        Assert.Null(settings.FactoryResetPinHash);
    }

    [Fact]
    public void Apply_change_requires_current_pin()
    {
        var settings = new StoreSettings();
        Assert.Null(ResetPinRules.Apply(settings, null, "1111", "1111"));

        var wrong = ResetPinRules.Apply(settings, "0000", "2222", "2222");
        Assert.Equal("الرقم السري الحالي غير صحيح.", wrong);
        Assert.True(ResetPinRules.Matches("1111", settings.FactoryResetPinHash));

        var missing = ResetPinRules.Apply(settings, null, "2222", "2222");
        Assert.Equal("الرقم السري الحالي غير صحيح.", missing);

        Assert.Null(ResetPinRules.Apply(settings, "1111", "2222", "2222"));
        Assert.True(ResetPinRules.Matches("2222", settings.FactoryResetPinHash));
    }
}
