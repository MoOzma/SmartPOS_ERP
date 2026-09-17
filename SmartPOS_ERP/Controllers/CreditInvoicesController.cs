using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Controllers;

[Authorize]
public class CreditInvoicesController : Controller
{
    private readonly CreditInvoiceService _creditInvoices;

    public CreditInvoicesController(CreditInvoiceService creditInvoices)
    {
        _creditInvoices = creditInvoices;
    }

    [HttpGet]
    public async Task<IActionResult> Open()
    {
        var invoices = await _creditInvoices.ListOpenAsync();
        return Json(invoices.Select(MapList));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _creditInvoices.GetAsync(id);
        if (invoice == null)
        {
            return NotFound();
        }

        return Json(Map(invoice));
    }

    [HttpGet]
    public async Task<IActionResult> Customers(string? q)
        => Json((await _creditInvoices.SearchCustomersAsync(q)).Select(c => new { c.Id, c.Name, c.Phone }));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateCreditRequest request)
    {
        var result = await _creditInvoices.CreateAsync(
            User.Identity?.Name,
            request.CustomerName,
            request.Phone,
            request.Lines ?? []);
        return await ActionJson(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLine(int id, int productId, decimal quantity)
        => await ActionJson(await _creditInvoices.AddLineAsync(User.Identity?.Name, id, productId, quantity));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnLine(int id, int productId, decimal quantity)
        => await ActionJson(await _creditInvoices.ReturnLineAsync(User.Identity?.Name, id, productId, quantity));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id, decimal amount)
        => await ActionJson(await _creditInvoices.RecordPaymentAsync(User.Identity?.Name, id, amount));

    private async Task<IActionResult> ActionJson(CreditActionResult result)
    {
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        object? invoice = null;
        if (result.InvoiceId is int invoiceId && !result.Settled)
        {
            var loaded = await _creditInvoices.GetAsync(invoiceId);
            if (loaded != null && loaded.Status == CreditInvoiceStatuses.Open)
            {
                invoice = Map(loaded);
            }
        }

        return Json(new
        {
            success = true,
            message = result.Message,
            invoiceId = result.InvoiceId,
            orderId = result.OrderId,
            settled = result.Settled,
            invoice
        });
    }

    private static object MapList(CreditInvoice invoice) => new
    {
        invoice.Id,
        invoice.CustomerName,
        invoice.CreatedAt,
        invoice.TotalAmount,
        paid = invoice.PaidAmount,
        remaining = invoice.RemainingAmount
    };

    private static object Map(CreditInvoice invoice) => new
    {
        invoice.Id,
        invoice.CustomerName,
        invoice.Status,
        invoice.CreatedAt,
        invoice.TotalAmount,
        invoice.TaxAmount,
        paid = invoice.PaidAmount,
        remaining = invoice.RemainingAmount,
        lines = invoice.Details.Select(d => new
        {
            id = d.ProductId,
            name = d.Product?.Name ?? "",
            qty = d.Quantity,
            price = d.UnitPrice,
            tax = d.TaxRate,
            unit = d.Product?.Unit ?? "Piece",
            track = d.Product?.TrackInventory ?? true,
            stock = d.Product?.StockQuantity ?? 0
        })
    };
}

public class CreateCreditRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public List<CreditLineInput> Lines { get; set; } = [];
}
