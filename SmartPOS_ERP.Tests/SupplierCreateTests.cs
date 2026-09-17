using Microsoft.AspNetCore.Mvc;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Tests;

public class SupplierCreateTests
{
    [Fact]
    public async Task CreateSupplier_get_returns_form()
    {
        await using var harness = new PosHarness();
        var result = harness.PurchasesController().CreateSupplier();
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task CreateSupplier_post_adds_supplier_and_redirects_to_list()
    {
        await using var harness = new PosHarness();
        var result = await harness.PurchasesController().CreateSupplier(new Supplier
        {
            Name = "مورد جديد",
            Phone = "0100",
            Invoices = [],
            Payments = []
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SmartPOS_ERP.Controllers.PurchasesController.Suppliers), redirect.ActionName);
        var supplier = Assert.Single(harness.Db.Suppliers);
        Assert.Equal("مورد جديد", supplier.Name);
        Assert.Equal("0100", supplier.Phone);
    }

    [Fact]
    public async Task CreateSupplier_post_rejects_empty_name()
    {
        await using var harness = new PosHarness();
        var controller = harness.PurchasesController();
        controller.ModelState.AddModelError("Name", "اسم المورد مطلوب");
        var result = await controller.CreateSupplier(new Supplier { Name = "", Invoices = [], Payments = [] });
        Assert.IsType<ViewResult>(result);
        Assert.Empty(harness.Db.Suppliers);
    }
}
