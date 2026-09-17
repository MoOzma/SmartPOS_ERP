using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Security;

public static class BootstrapAdmin
{
    public const string Username = "Admin";
    public const string Pin = "0000";
    public const string DisplayName = "المدير";

    public static User Create() => new()
    {
        Username = Username,
        DisplayName = DisplayName,
        Password = BCrypt.Net.BCrypt.HashPassword(Pin),
        Role = "Admin",
        IsActive = true
    };

    public static User? Find(ApplicationDbContext db) =>
        db.Users.FirstOrDefault(u => u.Username.ToLower() == Username.ToLower());

    public static User Ensure(ApplicationDbContext db)
    {
        var admin = Find(db);
        if (admin == null)
        {
            admin = Create();
            db.Users.Add(admin);
            db.SaveChanges();
            return admin;
        }

        var changed = false;
        if (!string.Equals(admin.Username, Username, StringComparison.Ordinal))
        {
            admin.Username = Username;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(admin.DisplayName))
        {
            admin.DisplayName = DisplayName;
            changed = true;
        }

        if (!string.Equals(admin.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            admin.Role = "Admin";
            changed = true;
        }

        if (!admin.IsActive)
        {
            admin.IsActive = true;
            changed = true;
        }

        if (changed)
        {
            db.SaveChanges();
        }

        return admin;
    }

    public static User ApplyInitialPin(ApplicationDbContext db)
    {
        var admin = Ensure(db);
        admin.Password = BCrypt.Net.BCrypt.HashPassword(Pin);
        db.SaveChanges();
        return admin;
    }
}
