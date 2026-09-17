using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartPOS_ERP.Models
{
    public class StockLedger
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public DateTime OccurredAt { get; set; } = DateTime.Now;

        [MaxLength(32)]
        public string MovementType { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18, 3)")]
        public decimal QuantityChange { get; set; }

        [Column(TypeName = "decimal(18, 3)")]
        public decimal QuantityBefore { get; set; }

        [Column(TypeName = "decimal(18, 3)")]
        public decimal QuantityAfter { get; set; }

        [MaxLength(200)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Reference { get; set; }

        [MaxLength(100)]
        public string? UserName { get; set; }

        public int? ShiftId { get; set; }
        public Shift? Shift { get; set; }
    }

    public static class StockMovementTypes
    {
        public const string Sale = "Sale";
        public const string Purchase = "Purchase";
        public const string Return = "Return";
        public const string PurchaseReturn = "PurchaseReturn";
        public const string Adjustment = "Adjustment";
        public const string Opening = "Opening";
    }
}
