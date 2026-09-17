using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SmartPOS_ERP.Controllers;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Tests;

public class LoginPickerTests
{
    [Fact]
    public async Task Login_get_lists_active_users_only()
    {
        await using var harness = new PosHarness();
        harness.Db.Users.AddRange(
            new User
            {
                Username = "sara",
                DisplayName = "سارة",
                Password = "hashed",
                Role = "Cashier",
                IsActive = true
            },
            new User
            {
                Username = "old",
                DisplayName = "قديم",
                Password = "hashed",
                Role = "Cashier",
                IsActive = false
            });
        await harness.Db.SaveChangesAsync();

        var controller = new AccountController(harness.Db, NullLogger<AccountController>.Instance);
        var result = controller.Login();

        Assert.IsType<ViewResult>(result);
        var users = Assert.IsAssignableFrom<IEnumerable<LoginUserOption>>((object)controller.ViewBag.LoginUsers).ToList();
        Assert.Contains(users, u => u.Username == "sara" && u.DisplayName == "سارة");
        Assert.DoesNotContain(users, u => u.Username == "old");
    }

    [Fact]
    public async Task Login_post_keeps_user_list_when_credentials_fail()
    {
        await using var harness = new PosHarness();
        harness.Db.Users.Add(new User
        {
            Username = "sara",
            DisplayName = "سارة",
            Password = BCrypt.Net.BCrypt.HashPassword("1234"),
            Role = "Cashier",
            IsActive = true
        });
        await harness.Db.SaveChangesAsync();

        var controller = new AccountController(harness.Db, NullLogger<AccountController>.Instance);
        var result = controller.Login("sara", "0000");

        Assert.IsType<ViewResult>(result);
        var users = Assert.IsAssignableFrom<IEnumerable<LoginUserOption>>((object)controller.ViewBag.LoginUsers).ToList();
        Assert.Contains(users, u => u.Username == "sara");
    }
}
