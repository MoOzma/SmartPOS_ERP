using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Security;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Controllers;

[Authorize(Policy = AppPermissions.Products)]
public class InventoryController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly StockLedgerService _stockLedger;

    public InventoryController(ApplicationDbContext context, StockLedgerService stockLedger)
    {
        _context = context;
        _stockLedger = stockLedger;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.OpenShiftId = await _context.GetOpenShiftIdAsync(User.Identity?.Name);
        ViewBag.Recent = await _context.StockLedgers
            .Include(l => l.Product)
            .Where(l => l.MovementType == StockMovementTypes.Adjustment)
            .OrderByDescending(l => l.OccurredAt)
            .Take(30)
            .ToListAsync();
        var products = await _context.Products
            .Where(p => p.TrackInventory)
            .OrderBy(p => p.Name)
            .ToListAsync();
        return View(products);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(AdjustStockRequest request)
    {
        var result = await ProductsController.TryAdjustStockAsync(_context, _stockLedger, User.Identity?.Name, request);
        TempData[result.Ok ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
