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
        public decimal DiscountAmount { get; set; }

        [MaxLength(20)]
        public string PaymentMethod { get; set; } = PaymentMethods.Cash;

        public int? ShiftId { get; set; }
        public Shift? Shift { get; set; }

        public bool IsCreditSettlement { get; set; }

        // قائمة بجميع الأصناف داخل هذه الفاتورة
        public List<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }
}