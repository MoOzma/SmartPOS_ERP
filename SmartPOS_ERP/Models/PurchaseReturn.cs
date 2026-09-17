using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartPOS_ERP.Models;

public class PurchaseReturn
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public int PurchaseInvoiceId { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }
    public DateTime ReturnDate { get; set; } = DateTime.Now;
    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    [MaxLength(64)]
    public string? UserName { get; set; }

    [MaxLength(200)]
    public string? Notes { get; set; }

    public List<PurchaseReturnDetail> Details { get; set; } = [];
}

public class PurchaseReturnDetail
{
    public int Id { get; set; }
    public int PurchaseReturnId { get; set; }
    public PurchaseReturn? PurchaseReturn { get; set; }
    public int PurchaseDetailId { get; set; }
    public PurchaseDetail? PurchaseDetail { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitCost { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal LineTotal { get; set; }
}

public class PurchaseReturnForm
{
    public string? Notes { get; set; }
    public List<PurchaseReturnLineForm> Lines { get; set; } = [];
}

public class PurchaseReturnLineForm
{
    public int PurchaseDetailId { get; set; }
    public decimal Quantity { get; set; }
}
