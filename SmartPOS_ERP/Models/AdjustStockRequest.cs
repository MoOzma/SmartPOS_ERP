using System.ComponentModel.DataAnnotations;

namespace SmartPOS_ERP.Models;

public class AdjustStockRequest
{
    public int ProductId { get; set; }

    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "الكمية لا يمكن أن تكون سالبة.")]
    public decimal NewQuantity { get; set; }

    public string Reason { get; set; } = StockAdjustmentReasons.Count;
    public string? Notes { get; set; }
}
