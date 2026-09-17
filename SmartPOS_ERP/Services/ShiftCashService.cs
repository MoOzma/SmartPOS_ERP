using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Services;

public class ShiftCashService
{
    private readonly ApplicationDbContext _db;

    public ShiftCashService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> ExpectedAsync(int shiftId, decimal openingCash, CancellationToken cancellationToken = default)
    {
        var shiftOrders = await _db.Orders
            .Where(o => o.ShiftId == shiftId
                && !o.IsCreditSettlement
                && (o.PaymentMethod == null || o.PaymentMethod == "" || o.PaymentMethod == PaymentMethods.Cash))
            .Select(o => new { o.Id, o.TotalAmount })
            .ToListAsync(cancellationToken);

        var orderIds = shiftOrders.Select(o => o.Id).ToList();
        var refundsOnShiftSales = 0m;
        if (orderIds.Count > 0)
        {
            refundsOnShiftSales = await _db.SalesReturns
                .Where(r => orderIds.Contains(r.OrderId))
                .SumAsync(r => (decimal?)r.RefundAmount, cancellationToken) ?? 0m;
        }

        var salesGross = shiftOrders.Sum(o => o.TotalAmount) + refundsOnShiftSales;
        var returnsThisShift = await _db.SalesReturns
            .Where(r => r.ShiftId == shiftId)
            .SumAsync(r => (decimal?)r.RefundAmount, cancellationToken) ?? 0m;
        var creditPays = await _db.CreditPayments
            .Where(p => p.ShiftId == shiftId)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var cashExpenses = await _db.Expenses
            .Where(e => e.ShiftId == shiftId && e.FromCash)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;
        var cashSupplierPays = await _db.SupplierPayments
            .Where(p => p.ShiftId == shiftId && p.FromCash)
            .SumAsync(p => (decimal?)p.AmountPaid, cancellationToken) ?? 0m;

        return openingCash + salesGross - returnsThisShift + creditPays - cashExpenses - cashSupplierPays;
    }

    public async Task<decimal> ExpectedForOpenShiftsAsync(CancellationToken cancellationToken = default)
    {
        var openShifts = await _db.Shifts
            .Where(s => s.ClosedAt == null)
            .Select(s => new { s.Id, s.OpeningCash })
            .ToListAsync(cancellationToken);

        var total = 0m;
        foreach (var shift in openShifts)
        {
            total += await ExpectedAsync(shift.Id, shift.OpeningCash, cancellationToken);
        }

        return total;
    }
}
