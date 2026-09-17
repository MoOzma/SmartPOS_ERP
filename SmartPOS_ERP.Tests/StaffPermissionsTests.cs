using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Security;

namespace SmartPOS_ERP.Tests;

public class StaffPermissionsTests
{
    [Fact]
    public void Admin_has_every_permission_without_flags()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "Admin")], "Test"));

        Assert.True(AppPermissions.Has(user, AppPermissions.Return));
        Assert.True(AppPermissions.Has(user, AppPermissions.Dashboard));
    }

    [Fact]
    public void Cashier_needs_matching_claim()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "Cashier"),
                new Claim(AppPermissions.ClaimType, AppPermissions.Return)
            ], "Test"));

        Assert.True(AppPermissions.Has(user, AppPermissions.Return));
        Assert.False(AppPermissions.Has(user, AppPermissions.Dashboard));
    }

    [Fact]
    public void StaffRules_blocks_deactivating_self_and_last_admin()
    {
        var admin = new User { Id = 1, Role = "Admin", IsActive = true };

        Assert.False(StaffRules.CanDeactivate(admin, currentUserId: 1, activeAdminCount: 2));
        Assert.False(StaffRules.CanDeactivate(admin, currentUserId: 9, activeAdminCount: 1));
        Assert.True(StaffRules.CanDeactivate(admin, currentUserId: 9, activeAdminCount: 2));
    }

    [Fact]
    public void StaffRules_blocks_demoting_last_admin()
    {
        var admin = new User { Id = 1, Role = "Admin", IsActive = true };
        Assert.False(StaffRules.CanChangeRoleToCashier(admin, activeAdminCount: 1));
        Assert.True(StaffRules.CanChangeRoleToCashier(admin, activeAdminCount: 2));
    }

    [Fact]
    public void Login_rejects_inactive_account()
    {
        var user = new User
        {
            Username = "sara",
            Password = BCrypt.Net.BCrypt.HashPassword("StrongPass1"),
            Role = "Cashier",
            IsActive = false,
            DisplayName = "سارة"
        };

        Assert.Equal("هذا الحساب موقوف", StaffAuth.LoginFailure(user, "StrongPass1"));
        Assert.Null(StaffAuth.LoginFailure(new User
        {
            Username = "sara",
            Password = user.Password,
            Role = "Cashier",
            IsActive = true,
            DisplayName = "سارة"
        }, "StrongPass1"));
    }

    [Fact]
    public async Task ProcessReturn_rejects_cashier_without_return_permission()
    {
        await using var harness = new PosHarness();
        var product = harness.SeedProduct(salePrice: 50m, costPrice: 20m, stock: 10m, taxRate: 0m);
        var sale = await harness.ProductsController().SaveOrder(new OrderViewModel
        {
            OrderDetails = [new OrderDetailViewModel { ProductId = product.Id, Quantity = 1m }]
        });
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(sale);

        var order = harness.Db.Orders.Single();
        var result = await harness.OrderController(role: "Cashier").ProcessReturn(order.Id, product.Id, 1m);
        var json = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(result);
        Assert.Equal(false, json.Value?.GetType().GetProperty("success")?.GetValue(json.Value));
    }

    [Fact]
    public async Task Cashier_without_all_orders_sees_only_own_shift_invoices()
    {
        await using var harness = new PosHarness();
        var me = new User { Username = "me", DisplayName = "أنا", Password = "hashed", Role = "Cashier", IsActive = true };
        var other = new User { Username = "other", DisplayName = "آخر", Password = "hashed", Role = "Cashier", IsActive = true };
        harness.Db.Users.AddRange(me, other);
        await harness.Db.SaveChangesAsync();

        var myShift = new Shift { UserId = me.Id, OpenedAt = DateTime.Now };
        var otherShift = new Shift { UserId = other.Id, OpenedAt = DateTime.Now };
        harness.Db.Shifts.AddRange(myShift, otherShift);
        await harness.Db.SaveChangesAsync();

        harness.Db.Orders.AddRange(
            new Order { ShiftId = myShift.Id, TotalAmount = 10m, OrderDate = DateTime.Now },
            new Order { ShiftId = otherShift.Id, TotalAmount = 20m, OrderDate = DateTime.Now },
            new Order { ShiftId = null, TotalAmount = 30m, OrderDate = DateTime.Now });
        await harness.Db.SaveChangesAsync();

        var own = Assert.IsType<ViewResult>(
            await harness.OrderController("Cashier").Index(null, null, null));
        var ownOrders = Assert.IsAssignableFrom<IEnumerable<Order>>(own.Model).ToList();
        Assert.Single(ownOrders);
        Assert.Equal(myShift.Id, ownOrders[0].ShiftId);

        var all = Assert.IsType<ViewResult>(
            await harness.OrderController("Cashier", AppPermissions.AllOrders).Index(null, null, null));
        var allOrders = Assert.IsAssignableFrom<IEnumerable<Order>>(all.Model);
        Assert.Equal(3, allOrders.Count());
    }
}
