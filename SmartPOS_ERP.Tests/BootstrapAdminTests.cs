using SmartPOS_ERP.Security;

namespace SmartPOS_ERP.Tests;

public class BootstrapAdminTests
{
    [Fact]
    public async Task Ensure_creates_admin_with_initial_pin_when_missing()
    {
        await using var harness = new PosHarness();
        harness.Db.Users.RemoveRange(harness.Db.Users);
        await harness.Db.SaveChangesAsync();

        BootstrapAdmin.Ensure(harness.Db);

        var admin = harness.Db.Users.Single(u => u.Username == BootstrapAdmin.Username);
        Assert.Equal("Admin", admin.Username);
        Assert.Equal("المدير", admin.DisplayName);
        Assert.Equal("Admin", admin.Role);
        Assert.True(admin.IsActive);
        Assert.Null(StaffAuth.LoginFailure(admin, BootstrapAdmin.Pin));
    }

    [Fact]
    public async Task Ensure_does_not_duplicate_existing_admin()
    {
        await using var harness = new PosHarness();
        harness.Db.Users.RemoveRange(harness.Db.Users);
        harness.Db.Users.Add(new SmartPOS_ERP.Models.User
        {
            Username = "admin",
            DisplayName = "مدير قديم",
            Password = BCrypt.Net.BCrypt.HashPassword("1234"),
            Role = "Admin",
            IsActive = true
        });
        await harness.Db.SaveChangesAsync();

        BootstrapAdmin.Ensure(harness.Db);
        BootstrapAdmin.Ensure(harness.Db);

        Assert.Equal(1, harness.Db.Users.Count(u => u.Username.ToLower() == "admin"));
        Assert.Equal("Admin", harness.Db.Users.Single().Username);
    }

    [Fact]
    public async Task ApplyInitialPin_sets_known_startup_pin()
    {
        await using var harness = new PosHarness();
        harness.Db.Users.RemoveRange(harness.Db.Users);
        harness.Db.Users.Add(new SmartPOS_ERP.Models.User
        {
            Username = "admin",
            DisplayName = "المدير",
            Password = BCrypt.Net.BCrypt.HashPassword("9999"),
            Role = "Admin",
            IsActive = true
        });
        await harness.Db.SaveChangesAsync();

        BootstrapAdmin.ApplyInitialPin(harness.Db);

        var admin = harness.Db.Users.Single();
        Assert.Equal("Admin", admin.Username);
        Assert.Null(StaffAuth.LoginFailure(admin, "0000"));
        Assert.Equal(StaffAuth.WrongCredentials, StaffAuth.LoginFailure(admin, "9999"));
    }
}
