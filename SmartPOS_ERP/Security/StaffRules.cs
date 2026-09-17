using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Security;

public static class StaffRules
{
    public static bool CanDeactivate(User target, int currentUserId, int activeAdminCount)
    {
        if (target.Id == currentUserId)
        {
            return false;
        }

        if (IsLastActiveAdmin(target, activeAdminCount))
        {
            return false;
        }

        return true;
    }

    public static bool CanChangeRoleToCashier(User target, int activeAdminCount)
        => !IsLastActiveAdmin(target, activeAdminCount);

    private static bool IsLastActiveAdmin(User target, int activeAdminCount)
        => target.Role == "Admin" && target.IsActive && activeAdminCount <= 1;
}
