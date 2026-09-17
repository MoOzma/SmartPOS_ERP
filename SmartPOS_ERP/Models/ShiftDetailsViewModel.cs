namespace SmartPOS_ERP.Models
{
    public class ShiftDetailsViewModel
    {
        public Shift Shift { get; set; } = null!;
        public IReadOnlyList<Order> Orders { get; set; } = Array.Empty<Order>();
        public IReadOnlyList<SalesReturn> Returns { get; set; } = Array.Empty<SalesReturn>();
        public IReadOnlyList<StockLedger> StockMovements { get; set; } = Array.Empty<StockLedger>();
        public decimal SalesTotal { get; set; }
        public decimal ReturnsTotal { get; set; }
    }
}
