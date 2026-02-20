using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartPOS_ERP.Models
{
    public enum UnitType { Piece, Kilo }
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        public string Name { get; set; }
        public UnitType Unit { get; set; } // قطعة أو كيلو
        public string? Barcode { get; set; }

        // --- الإضافات الضرورية للتقارير والأرباح ---

        [Required]
        [Display(Name = "سعر التكلفة")]
        public decimal CostPrice { get; set; } // ضروري لحساب الربح (سعر البيع - سعر التكلفة)

        [Required]
        [Display(Name = "سعر البيع")]
        public decimal SalePrice { get; set; } // السعر الذي يدفعه العميل

        public decimal TaxRate { get; set; } = 0.14m; // ضريبة القيمة المضافة

        // --- تتبع المخزون ---

        public bool TrackInventory { get; set; } = true;

        [Display(Name = "الكمية الحالية")]
        [Column(TypeName = "decimal(18, 3)")] // لدقة تصل لـ 3 علامات عشرية (جرامات)
        public decimal StockQuantity { get; set; }

        [Display(Name = "حد إعادة الطلب")]
        public int ReorderLevel { get; set; } // ينبهك النظام عندما توشك البضاعة على النفاذ


        public string? ImagePath { get; set; }
    }
}