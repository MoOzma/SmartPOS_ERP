using SmartPOS_ERP.Security;

namespace SmartPOS_ERP.Tests;

public class PasswordRulesTests
{
    [Theory]
    [InlineData("1234")]
    [InlineData("0000")]
    [InlineData("9876")]
    public void TryValidate_accepts_four_digits(string pin)
    {
        Assert.True(PasswordRules.TryValidate(pin, out var error));
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
    [InlineData("StrongPass1")]
    public void TryValidate_rejects_non_four_digit_pin(string? pin)
    {
        Assert.False(PasswordRules.TryValidate(pin, out var error));
        Assert.Equal("الرقم السري يجب أن يكون 4 أرقام.", error);
    }
}
