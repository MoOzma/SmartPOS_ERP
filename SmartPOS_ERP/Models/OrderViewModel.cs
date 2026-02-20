namespace SmartPOS_ERP.Models
{
    public class OrderViewModel
    {
        public decimal TotalAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public List<OrderDetailViewModel> OrderDetails { get; set; }
    }
}
