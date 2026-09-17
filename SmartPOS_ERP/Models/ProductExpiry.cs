namespace SmartPOS_ERP.Models;

public static class ProductExpiry
{
    public const int WarningDays = 7;

    public static DateTime ExclusiveEnd(DateTime today) => today.Date.AddDays(WarningDays + 1);

    public static bool IsWatched(DateTime? expiry, DateTime today)
        => expiry is DateTime date && date < ExclusiveEnd(today);

    public static string StatusLabel(DateTime expiry, DateTime today)
    {
        if (expiry.Date < today.Date) return "منتهية";
        if (expiry.Date == today.Date) return "تنتهي اليوم";
        return "قربت تنتهي";
    }
}
