namespace SmartPOS_ERP.Models;

public static class ProductCategories
{
    public static readonly string[] Suggestions =
    [
        "مشروبات",
        "ألبان",
        "حلويات",
        "مخبوزات",
        "معلبات",
        "منظفات",
        "أخرى"
    ];

    public static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
