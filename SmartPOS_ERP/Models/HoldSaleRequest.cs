namespace SmartPOS_ERP.Models;

public class HoldSaleRequest
{
    public string PayloadJson { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public decimal TotalAmount { get; set; }
}
