using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartPOS_ERP.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        public string Name { get; set; }
       
        [MaxLength(64)]
        public string? Barcode { get; set; }

        [Display(Name = "تخصيص نسبة لهذا الصنف")]
        public bool UseCustomTax { get; set; }

        [Display(Name = "نسبة الضريبة %")]
        [Column(TypeName = "decimal(18, 2)")]
        [Range(0, 100, ErrorMessage = "النسبة يجب أن تكون بين 0 و 100.")]
        public decimal TaxRate { get; set; }

        // --- الإضافات الضرورية للتقارير والأرباح ---

        [Required]
        [Display(Name = "سعر التكلفة")]
        [Range(typeof(decimal), "0.01", "999999999", ErrorMessage = "سعر التكلفة يجب أن يكون أكبر من صفر.")]
        public decimal CostPrice { get; set; }

        [Required]
        [Display(Name = "سعر البيع")]
        [Range(typeof(decimal), "0.01", "999999999", ErrorMessage = "سعر البيع يجب أن يكون أكبر من صفر.")]
        public decimal SalePrice { get; set; }


        // --- تتبع المخزون ---

        public bool TrackInventory { get; set; } = true;

        [Display(Name = "الكمية الحالية")]
        [Column(TypeName = "decimal(18, 3)")]
        [Range(typeof(decimal), "0", "999999999", ErrorMessage = "الكمية لا يمكن أن تكون سالبة.")]
        public decimal StockQuantity { get; set; }

        [Display(Name = "حد إعادة الطلب")]
        [Range(0, int.MaxValue, ErrorMessage = "حد إعادة الطلب لا يمكن أن يكون سالباً.")]
        public int ReorderLevel { get; set; }

        [Required(ErrorMessage = "وحدة البيع مطلوبة")]
        [RegularExpression("^(Piece|Kilo)$", ErrorMessage = "وحدة البيع يجب أن تكون قطعة أو كيلو.")]
        public string Unit { get; set; } 


        public string? ImagePath { get; set; }

        public bool IsPinned { get; set; }

        [MaxLength(50)]
        [Display(Name = "القسم")]
        public string? Category { get; set; }

        [Display(Name = "تاريخ الصلاحية")]
        [DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; }

        [NotMapped]
        [Display(Name = "عدد الكراتين")]
        public decimal? CartonCount { get; set; }

        [NotMapped]
        [Display(Name = "سعر الكرتون")]
        public decimal? CartonPrice { get; set; }

        [NotMapped]
        [Display(Name = "قطع في الكرتون")]
        public decimal? PiecesPerCarton { get; set; }

        [Timestamp]
        [BindNever]
        [ValidateNever]
        public byte[] RowVersion { get; set; } = null!;
    }
}