using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartPOS_ERP.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        public string Name { get; set; }
       
        public string? Barcode { get; set; }

        // --- الإضافات الضرورية للتقارير والأرباح ---

        [Required]
        [Display(Name = "سعر التكلفة")]
        public decimal CostPrice { get; set; } // ضروري لحساب الربح (سعر البيع - سعر التكلفة)

        [Required]
        [Display(Name = "سعر البيع")]
        public decimal SalePrice { get; set; } // السعر الذي يدفعه العميل


        // --- تتبع المخزون ---

        public bool TrackInventory { get; set; } = true;

        [Display(Name = "الكمية الحالية")]
        [Column(TypeName = "decimal(18, 3)")] // لدقة تصل لـ 3 علامات عشرية (جرامات)
        public decimal StockQuantity { get; set; }

        [Display(Name = "حد إعادة الطلب")]
        public int ReorderLevel { get; set; } // ينبهك النظام عندما توشك البضاعة على النفاذ
        
        public string Unit { get; set; } 


        public string? ImagePath { get; set; }
    }
}