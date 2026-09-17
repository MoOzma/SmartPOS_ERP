namespace SmartPOS_ERP.Models;

public static class ReturnReasons
{
    public const int NotesMaxLength = 200;

    public static readonly string[] All =
    [
        "العميل رجّع الصنف",
        "خطأ في البيع",
        "تالف / غير مطابق",
        "سعر غلط",
        "سبب آخر"
    ];

    public static string? Validate(string? reason, string? notes)
    {
        if (!string.IsNullOrWhiteSpace(reason) && !All.Contains(reason.Trim()))
        {
            return "سبب الارتجاع غير صحيح.";
        }

        if (!string.IsNullOrWhiteSpace(notes) && notes.Trim().Length > NotesMaxLength)
        {
            return "الملاحظات أطول من المسموح.";
        }

        return null;
    }

    public static (string? Reason, string? Notes) Normalize(string? reason, string? notes)
        => (
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim());
}

public static class ReturnQuantity
{
    public static bool IsWeight(string? unit)
        => string.Equals(unit, "Kilo", StringComparison.OrdinalIgnoreCase);

    public static string? Validate(string? unit, decimal qty, decimal sold)
    {
        if (qty <= 0)
        {
            return "الكمية المرتجعة يجب أن تكون أكبر من صفر.";
        }

        if (qty > sold)
        {
            return "الكمية المرتجعة أكبر من المباعة!";
        }

        if (!IsWeight(unit) && qty != decimal.Truncate(qty))
        {
            return "كمية العدد يجب أن تكون عددًا صحيحًا.";
        }

        return null;
    }
}
