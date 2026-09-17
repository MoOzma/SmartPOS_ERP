using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Services;

public class CreditInvoiceService
{
    private readonly ApplicationDbContext _db;
    private readonly StockLedgerService _stockLedger;
    private readonly StoreSettingsService _storeSettings;

    public CreditInvoiceService(
        ApplicationDbContext db,
        StockLedgerService stockLedger,
        StoreSettingsService storeSettings)
    {
        _db = db;
        _stockLedger = stockLedger;
        _storeSettings = storeSettings;
    }

    public async Task<CreditActionResult> CreateAsync(
        string? userName,
        string customerName,
        string? phone,
        IReadOnlyList<CreditLineInput> lines)
    {
        var name = (customerName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Fail("اسم العميل مطلوب.");
        }

        if (lines == null || lines.Count == 0 || lines.All(l => l.Quantity <= 0))
        {
            return Fail("أضف أصناف أولاً.");
        }

        var shiftId = await RequireShiftAsync(userName);
        if (shiftId is null)
        {
            return Fail("يجب فتح وردية أولاً قبل تسجيل فاتورة آجل.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var customer = await FindOrCreateCustomerAsync(name, phone);
            var settings = await _storeSettings.GetAsync();
            var invoice = new CreditInvoice
            {
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                CreatedAt = DateTime.Now,
                ShiftId = shiftId,
                Status = CreditInvoiceStatuses.Open
            };

            foreach (var group in lines.Where(l => l.Quantity > 0).GroupBy(l => l.ProductId))
            {
                var quantity = group.Sum(l => l.Quantity);
                var added = await TryAddStockLineAsync(invoice, group.Key, quantity, settings);
                if (added != null)
                {
                    await transaction.RollbackAsync();
                    return Fail(added);
                }
            }

            Recalc(invoice);
            if (invoice.Details.Count == 0)
            {
                await transaction.RollbackAsync();
                return Fail("أضف أصناف أولاً.");
            }

            _db.CreditInvoices.Add(invoice);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new CreditActionResult(true, "تم حفظ فاتورة الآجل.", invoice.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return Fail("تم تعديل المخزون من عملية أخرى. أعد المحاولة.");
        }
        catch
        {
            await transaction.RollbackAsync();
            return Fail("حدث خطأ أثناء حفظ فاتورة الآجل.");
        }
    }

    public async Task<CreditActionResult> AddLineAsync(string? userName, int invoiceId, int productId, decimal quantity)
    {
        if (quantity <= 0)
        {
            return Fail(InputRules.QuantityMustBePositive);
        }

        if (await RequireShiftAsync(userName) is null)
        {
            return Fail("يجب فتح وردية أولاً.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var invoice = await LoadOpenAsync(invoiceId);
            if (invoice == null)
            {
                await transaction.RollbackAsync();
                return Fail("فاتورة الآجل غير موجودة أو مغلقة.");
            }

            var settings = await _storeSettings.GetAsync();
            var added = await TryAddStockLineAsync(invoice, productId, quantity, settings);
            if (added != null)
            {
                await transaction.RollbackAsync();
                return Fail(added);
            }

            Recalc(invoice);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new CreditActionResult(true, "تمت إضافة الصنف.", invoice.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return Fail("تم تعديل المخزون من عملية أخرى. أعد المحاولة.");
        }
        catch
        {
            await transaction.RollbackAsync();
            return Fail("حدث خطأ أثناء إضافة الصنف.");
        }
    }

    public async Task<CreditActionResult> ReturnLineAsync(string? userName, int invoiceId, int productId, decimal quantity)
    {
        if (await RequireShiftAsync(userName) is null)
        {
            return Fail("يجب فتح وردية أولاً.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var invoice = await LoadOpenAsync(invoiceId);
            if (invoice == null)
            {
                await transaction.RollbackAsync();
                return Fail("فاتورة الآجل غير موجودة أو مغلقة.");
            }

            var line = invoice.Details.FirstOrDefault(d => d.ProductId == productId);
            if (line == null)
            {
                await transaction.RollbackAsync();
                return Fail("الصنف غير موجود في الفاتورة.");
            }

            var product = await _db.GetProductWithRowLockAsync(productId);
            if (product == null)
            {
                await transaction.RollbackAsync();
                return Fail("المنتج غير موجود.");
            }

            var qtyError = ReturnQuantity.Validate(product.Unit, quantity, line.Quantity);
            if (qtyError != null)
            {
                await transaction.RollbackAsync();
                return Fail(qtyError);
            }

            if (product.TrackInventory)
            {
                _stockLedger.ApplyDelta(product, quantity, StockMovementTypes.Return, "مرتجع آجل", $"فاتورة آجل #{invoice.Id}");
            }

            line.Quantity -= quantity;
            if (line.Quantity <= 0)
            {
                invoice.Details.Remove(line);
                _db.CreditInvoiceDetails.Remove(line);
            }

            Recalc(invoice);
            var closed = TryCloseIfZero(invoice);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new CreditActionResult(true, closed ? "تم إلغاء فاتورة الآجل بعد ارتجاع كل الأصناف." : "تم تسجيل المرتجع.", invoice.Id, Settled: closed);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return Fail("تم تعديل المخزون من عملية أخرى. أعد المحاولة.");
        }
        catch
        {
            await transaction.RollbackAsync();
            return Fail("حدث خطأ أثناء المرتجع.");
        }
    }

    public async Task<CreditActionResult> RecordPaymentAsync(string? userName, int invoiceId, decimal amount, string? notes = null)
    {
        if (amount <= 0)
        {
            return Fail(InputRules.AmountMustBePositive);
        }

        var shiftId = await RequireShiftAsync(userName);
        if (shiftId is null)
        {
            return Fail("يجب فتح وردية أولاً قبل تسجيل دفعة.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var invoice = await LoadOpenAsync(invoiceId);
            if (invoice == null)
            {
                await transaction.RollbackAsync();
                return Fail("فاتورة الآجل غير موجودة أو مغلقة.");
            }

            Recalc(invoice);
            if (amount > invoice.RemainingAmount)
            {
                await transaction.RollbackAsync();
                return Fail("المبلغ أكبر من المتبقي على الفاتورة.");
            }

            invoice.Payments.Add(new CreditPayment
            {
                Amount = amount,
                PaidAt = DateTime.Now,
                ShiftId = shiftId,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
            });

            int? orderId = null;
            var settled = false;
            if (invoice.RemainingAmount <= 0)
            {
                if (invoice.TotalAmount <= 0)
                {
                    invoice.Status = CreditInvoiceStatuses.Cancelled;
                }
                else
                {
                    orderId = await ConvertToOrderAsync(invoice);
                    invoice.Status = CreditInvoiceStatuses.Settled;
                    invoice.OrderId = orderId;
                    settled = true;
                }
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new CreditActionResult(
                true,
                settled ? "تم تحصيل الفاتورة وتحويلها لبيع." : "تم تسجيل الدفعة.",
                invoice.Id,
                orderId,
                settled);
        }
        catch
        {
            await transaction.RollbackAsync();
            return Fail("حدث خطأ أثناء تسجيل الدفعة.");
        }
    }

    public Task<List<CreditInvoice>> ListOpenAsync()
        => _db.CreditInvoices
            .AsNoTracking()
            .Include(i => i.Payments)
            .Where(i => i.Status == CreditInvoiceStatuses.Open)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

    public Task<CreditInvoice?> GetAsync(int id)
        => _db.CreditInvoices
            .Include(i => i.Details)
                .ThenInclude(d => d.Product)
            .Include(i => i.Payments)
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.Id == id);

    public Task<List<Customer>> SearchCustomersAsync(string? query)
    {
        var q = (query ?? "").Trim();
        var customers = _db.Customers.AsNoTracking().OrderBy(c => c.Name).AsQueryable();
        if (!string.IsNullOrEmpty(q))
        {
            customers = customers.Where(c => c.Name.Contains(q) || (c.Phone != null && c.Phone.Contains(q)));
        }

        return customers.Take(20).ToListAsync();
    }

    private async Task<string?> TryAddStockLineAsync(CreditInvoice invoice, int productId, decimal quantity, StoreSettings settings)
    {
        var product = await _db.GetProductWithRowLockAsync(productId);
        if (product == null)
        {
            return "أحد الأصناف غير موجود.";
        }

        if (!InputRules.IsPositive(product.SalePrice))
        {
            return InputRules.SalePriceMustBePositive;
        }

        if (product.TrackInventory && product.StockQuantity < quantity)
        {
            var unitName = product.Unit == "Kilo" ? "كجم" : "قطعة";
            return $"خطأ في كمية {product.Name}: المطلوب ({quantity} {unitName})، والمتاح في المخزن ({product.StockQuantity} {unitName}) فقط!";
        }

        if (product.TrackInventory)
        {
            _stockLedger.ApplyDelta(product, -quantity, StockMovementTypes.Sale, "بيع آجل", $"فاتورة آجل{(invoice.Id > 0 ? $" #{invoice.Id}" : "")}");
        }

        var existing = invoice.Details.FirstOrDefault(d => d.ProductId == productId);
        if (existing != null)
        {
            existing.Quantity += quantity;
            return null;
        }

        invoice.Details.Add(new CreditInvoiceDetail
        {
            ProductId = product.Id,
            Quantity = quantity,
            UnitPrice = product.SalePrice,
            UnitCost = product.CostPrice,
            TaxRate = StoreSettingsService.EffectiveTaxRate(product, settings)
        });
        return null;
    }

    private async Task<int> ConvertToOrderAsync(CreditInvoice invoice)
    {
        var order = new Order
        {
            OrderDate = invoice.CreatedAt,
            TotalAmount = invoice.TotalAmount,
            TaxAmount = invoice.TaxAmount,
            ShiftId = invoice.ShiftId,
            IsCreditSettlement = true,
            OrderDetails = invoice.Details.Select(d => new OrderDetail
            {
                ProductId = d.ProductId,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                UnitCost = d.UnitCost,
                TaxRate = d.TaxRate
            }).ToList()
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return order.Id;
    }

    private async Task<Customer> FindOrCreateCustomerAsync(string name, string? phone)
    {
        phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.Name == name);
        if (existing != null)
        {
            if (phone != null && string.IsNullOrWhiteSpace(existing.Phone))
            {
                existing.Phone = phone;
            }

            return existing;
        }

        var customer = new Customer { Name = name, Phone = phone };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return customer;
    }

    private Task<CreditInvoice?> LoadOpenAsync(int id)
        => _db.CreditInvoices
            .Include(i => i.Details)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id && i.Status == CreditInvoiceStatuses.Open);

    private Task<int?> RequireShiftAsync(string? userName) => _db.GetOpenShiftIdAsync(userName);

    private static bool TryCloseIfZero(CreditInvoice invoice)
    {
        if (invoice.TotalAmount > 0)
        {
            return false;
        }

        invoice.Status = CreditInvoiceStatuses.Cancelled;
        return true;
    }

    private static void Recalc(CreditInvoice invoice)
    {
        decimal subtotal = 0;
        decimal tax = 0;
        foreach (var detail in invoice.Details)
        {
            var line = detail.Quantity * detail.UnitPrice;
            subtotal += line;
            tax += line * (detail.TaxRate / 100m);
        }

        invoice.TaxAmount = Math.Round(tax, 2);
        invoice.TotalAmount = subtotal + invoice.TaxAmount;
    }

    private static CreditActionResult Fail(string message) => new(false, message);
}
