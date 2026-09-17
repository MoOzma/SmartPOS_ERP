using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Security;

namespace SmartPOS_ERP.Controllers;

[Authorize]
public class CustomersController : Controller
{
    private readonly ApplicationDbContext _context;

    public CustomersController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var query = _context.Customers.Include(c => c.Invoices).ThenInclude(i => i.Payments).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.Name.Contains(term) || (c.Phone != null && c.Phone.Contains(term)));
        }

        ViewBag.Query = q;
        var customers = await query.OrderBy(c => c.Name).ToListAsync();
        return View(customers);
    }

    public IActionResult Create() => View(new Customer());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Phone,Notes")] Customer customer)
    {
        if (string.IsNullOrWhiteSpace(customer.Name))
        {
            ModelState.AddModelError(nameof(customer.Name), "اسم العميل مطلوب");
        }

        if (!ModelState.IsValid)
        {
            return View(customer);
        }

        customer.Name = customer.Name.Trim();
        customer.Phone = string.IsNullOrWhiteSpace(customer.Phone) ? null : customer.Phone.Trim();
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        return customer == null ? NotFound() : View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Phone,Notes")] Customer customer)
    {
        if (id != customer.Id)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(customer.Name))
        {
            ModelState.AddModelError(nameof(customer.Name), "اسم العميل مطلوب");
        }

        if (!ModelState.IsValid)
        {
            return View(customer);
        }

        var existing = await _context.Customers.FindAsync(id);
        if (existing == null)
        {
            return NotFound();
        }

        existing.Name = customer.Name.Trim();
        existing.Phone = string.IsNullOrWhiteSpace(customer.Phone) ? null : customer.Phone.Trim();
        existing.Notes = customer.Notes;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var customer = await _context.Customers
            .Include(c => c.Invoices)
                .ThenInclude(i => i.Payments)
            .Include(c => c.Invoices)
                .ThenInclude(i => i.Details)
            .FirstOrDefaultAsync(c => c.Id == id);
        return customer == null ? NotFound() : View(customer);
    }
}
