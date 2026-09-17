namespace SmartPOS_ERP.Models
{
    public class OrderViewModel
    {
        public decimal TotalAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? PaymentMethod { get; set; }
        public List<OrderDetailViewModel> OrderDetails { get; set; }
    }
}
