using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Security;

public static class ResetPinRules
{
    public const int Length = 4;
    public const string InvalidMessage = "الرقم السري يجب أن يكون 4 أرقام.";
    public const string ConfirmMismatchMessage = "تأكيد الرقم غير مطابق.";
    public const string WrongCurrentMessage = "الرقم السري الحالي غير صحيح.";

    public static bool TryValidate(string? pin, out string error)
    {
        if (string.IsNullOrEmpty(pin) || pin.Length != Length || !pin.All(char.IsDigit))
        {
            error = InvalidMessage;
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static string Hash(string pin) => BCrypt.Net.BCrypt.HashPassword(pin);

    public static bool Matches(string? pin, string? hash)
    {
        if (string.IsNullOrEmpty(hash) || pin is null)
        {
            return false;
        }

        return BCrypt.Net.BCrypt.Verify(pin, hash);
    }

    public static string? Apply(StoreSettings settings, string? currentPin, string? newPin, string? confirm)
    {
        if (!TryValidate(newPin, out var error))
        {
            return error;
        }

        if (newPin != confirm)
        {
            return ConfirmMismatchMessage;
        }

        if (!string.IsNullOrEmpty(settings.FactoryResetPinHash)
            && !Matches(currentPin, settings.FactoryResetPinHash))
        {
            return WrongCurrentMessage;
        }

        settings.FactoryResetPinHash = Hash(newPin!);
        return null;
    }
}
