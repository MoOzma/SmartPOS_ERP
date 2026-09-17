namespace SmartPOS_ERP.Models;

public static class PaymentMethods
{
    public const string Cash = "Cash";
    public const string Card = "Card";
    public const string Transfer = "Transfer";

    public static string Normalize(string? value) => value?.Trim() switch
    {
        Card => Card,
        Transfer => Transfer,
        _ => Cash
    };

    public static bool IsCash(string? value)
        => string.IsNullOrWhiteSpace(value) || string.Equals(value, Cash, StringComparison.OrdinalIgnoreCase);

    public static string Label(string? value) => Normalize(value) switch
    {
        Card => "بطاقة",
        Transfer => "تحويل",
        _ => "نقدي"
    };

    public static IReadOnlyList<string> Enabled(StoreSettings settings)
    {
        var methods = new List<string>(3);
        if (settings.EnablePaymentCash)
        {
            methods.Add(Cash);
        }

        if (settings.EnablePaymentCard)
        {
            methods.Add(Card);
        }

        if (settings.EnablePaymentTransfer)
        {
            methods.Add(Transfer);
        }

        if (methods.Count == 0)
        {
            methods.Add(Cash);
        }

        return methods;
    }

    public static bool IsAllowed(string? value, StoreSettings settings)
        => Enabled(settings).Contains(Normalize(value));

    public static string FirstEnabled(StoreSettings settings)
        => Enabled(settings)[0];
}
