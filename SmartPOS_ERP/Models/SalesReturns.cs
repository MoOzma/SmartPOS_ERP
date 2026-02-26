using System.ComponentModel.DataAnnotations;

namespace SmartPOS_ERP.Models
{
    
        public class SalesReturn
        {
            [Key]
            public int Id { get; set; }

            public int OrderId { get; set; } // الربط مع جدول الطلبات الأساسي
            public virtual Order Order { get; set; }

            public int ProductId { get; set; } // المنتج المرتجع
            public virtual Product Product { get; set; }

            [Display(Name = "الكمية المرتجعة")]
            public int Quantity { get; set; }

            [Display(Name = "المبلغ المسترد")]
            public decimal RefundAmount { get; set; }

            public DateTime ReturnDate { get; set; } = DateTime.Now;
        }
    
}
