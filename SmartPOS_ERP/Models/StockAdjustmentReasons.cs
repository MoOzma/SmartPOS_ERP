namespace SmartPOS_ERP.Models;

public static class StockAdjustmentReasons
{
    public const string Count = "Count";
    public const string Damage = "Damage";
    public const string Correction = "Correction";
    public const string Expired = "Expired";

    public static readonly (string Value, string Label)[] All =
    [
        (Count, "جرد / تسوية"),
        (Damage, "تلف"),
        (Correction, "تصحيح خطأ"),
        (Expired, "انتهاء صلاحية")
    ];

    public static bool IsKnown(string? value)
        => All.Any(x => string.Equals(x.Value, value, StringComparison.OrdinalIgnoreCase));

    public static string Label(string? value)
        => All.FirstOrDefault(x => string.Equals(x.Value, value, StringComparison.OrdinalIgnoreCase)).Label
           ?? "تعديل يدوي";
}
