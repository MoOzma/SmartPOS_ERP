using System.ComponentModel.DataAnnotations;

namespace SmartPOS_ERP.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public decimal TotalAmount { get; set; }
        public decimal TaxAmount { get; set; }
        // قائمة بجميع الأصناف داخل هذه الفاتورة
        public List<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }
}