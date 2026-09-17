using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartPOS_ERP.Models;

public static class CreditInvoiceStatuses
{
    public const string Open = "Open";
    public const string Settled = "Settled";
    public const string Cancelled = "Cancelled";
}

public class CreditInvoice
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = CreditInvoiceStatuses.Open;

    public decimal TotalAmount { get; set; }
    public decimal TaxAmount { get; set; }

    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    public List<CreditInvoiceDetail> Details { get; set; } = [];
    public List<CreditPayment> Payments { get; set; } = [];

    [NotMapped]
    public decimal PaidAmount => Payments?.Sum(p => p.Amount) ?? 0;

    [NotMapped]
    public decimal RemainingAmount => TotalAmount - PaidAmount;
}

public class CreditInvoiceDetail
{
    public int Id { get; set; }
    public int CreditInvoiceId { get; set; }
    public CreditInvoice? CreditInvoice { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TaxRate { get; set; }
}

public class CreditPayment
{
    public int Id { get; set; }
    public int CreditInvoiceId { get; set; }
    public CreditInvoice? CreditInvoice { get; set; }

    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.Now;
    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    [MaxLength(200)]
    public string? Notes { get; set; }
}

public class CreditLineInput
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
}

public sealed record CreditActionResult(
    bool Success,
    string Message,
    int? InvoiceId = null,
    int? OrderId = null,
    bool Settled = false);
