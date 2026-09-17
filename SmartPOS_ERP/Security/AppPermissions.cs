using System.Security.Claims;

namespace SmartPOS_ERP.Security;

public static class AppPermissions
{
    public const string ClaimType = "permission";
    public const string Return = "Return";
    public const string Products = "Products";
    public const string AllOrders = "AllOrders";
    public const string Expenses = "Expenses";
    public const string Purchases = "Purchases";
    public const string Dashboard = "Dashboard";

    public static readonly string[] All =
    [
        Return, Products, AllOrders, Expenses, Purchases, Dashboard
    ];

    public static bool Has(ClaimsPrincipal? user, string permission)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return user.IsInRole("Admin") || user.HasClaim(ClaimType, permission);
    }
}
