namespace SmartPOS_ERP.Models
{
    public class OrderDetailViewModel
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; } // تأكد أنها decimal لدعم الجرامات
        public decimal UnitPrice { get; set; }
    }
}
