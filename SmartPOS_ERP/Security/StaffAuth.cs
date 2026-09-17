using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Security;

public static class StaffAuth
{
    public const string WrongCredentials = "بيانات الدخول غير صحيحة";
    public const string AccountDisabled = "هذا الحساب موقوف";

    public static string? LoginFailure(User? user, string password)
    {
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.Password))
        {
            return WrongCredentials;
        }

        if (!user.IsActive)
        {
            return AccountDisabled;
        }

        return null;
    }

    public static IReadOnlyList<string> PermissionValues(User user)
    {
        if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var values = new List<string>(6);
        if (user.CanProcessReturn) values.Add(AppPermissions.Return);
        if (user.CanManageProducts) values.Add(AppPermissions.Products);
        if (user.CanViewAllOrders) values.Add(AppPermissions.AllOrders);
        if (user.CanManageExpenses) values.Add(AppPermissions.Expenses);
        if (user.CanManagePurchases) values.Add(AppPermissions.Purchases);
        if (user.CanViewDashboard) values.Add(AppPermissions.Dashboard);
        return values;
    }

    public static void ApplySession(ISession session, User user)
    {
        session.SetString("UserName", user.Username);
        session.SetString("UserRole", user.Role);
        session.SetString("UserId", user.Id.ToString());
        session.SetString("Permissions", string.Join(',', PermissionValues(user)));
    }
}
