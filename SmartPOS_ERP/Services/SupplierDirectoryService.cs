using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Services;

public class SupplierDirectoryService
{
    private readonly ApplicationDbContext _db;

    public SupplierDirectoryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SupplierDirectoryViewModel> BuildAsync(
        string? query,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var q = query?.Trim();
        var kind = status?.Trim().ToLowerInvariant() switch
        {
            "due" => "due",
            "settled" => "settled",
            _ => "all"
        };

        var suppliers = await _db.Suppliers
            .Include(s => s.Invoices)
                .ThenInclude(i => i.Details)
            .Include(s => s.Payments)
            .Include(s => s.Returns)
                .ThenInclude(r => r.Details)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        var rows = suppliers.Select(s =>
        {
            var invoices = (s.Invoices ?? []).Where(i => !i.IsVoided).ToList();
            var payments = s.Payments ?? [];
            var purchases = invoices.Sum(i => i.Details?.Sum(d => d.PackageQuantity * d.PackageCost) ?? 0);
            var paid = payments.Sum(p => p.AmountPaid);
            var returned = (s.Returns ?? []).SelectMany(r => r.Details ?? []).Sum(d => d.LineTotal);
            return new SupplierDirectoryRow
            {
                Id = s.Id,
                Name = s.Name,
                Phone = s.Phone,
                Address = s.Address,
                Notes = s.Notes,
                InvoiceCount = invoices.Count,
                LastInvoiceDate = invoices.Count == 0 ? null : invoices.Max(i => i.InvoiceDate),
                TotalPurchases = purchases,
                TotalPaid = paid,
                Balance = purchases - paid - returned
            };
        });

        if (!string.IsNullOrWhiteSpace(q))
        {
            rows = rows.Where(r =>
                Contains(r.Name, q) ||
                Contains(r.Phone, q) ||
                Contains(r.Address, q) ||
                Contains(r.Notes, q));
        }

        rows = kind switch
        {
            "due" => rows.Where(r => r.Balance > 0),
            "settled" => rows.Where(r => r.Balance <= 0),
            _ => rows
        };

        var items = rows
            .OrderByDescending(r => r.Balance)
            .ThenBy(r => r.Name)
            .ToList();

        return new SupplierDirectoryViewModel
        {
            Query = q,
            Status = kind,
            SupplierCount = items.Count,
            DueCount = items.Count(r => r.Balance > 0),
            TotalPurchases = items.Sum(r => r.TotalPurchases),
            TotalPaid = items.Sum(r => r.TotalPaid),
            TotalBalance = items.Sum(r => r.Balance),
            Items = items
        };
    }

    private static bool Contains(string? value, string query)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Contains(query, StringComparison.OrdinalIgnoreCase);
}
