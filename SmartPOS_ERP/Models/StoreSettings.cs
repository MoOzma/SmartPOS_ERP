using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SmartPOS_ERP.Models
{
    public class StoreSettings
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; } = 1;

        [Required(ErrorMessage = "اسم المحل مطلوب")]
        [MaxLength(80)]
        [Display(Name = "اسم المحل")]
        public string StoreName { get; set; } = "Sama_POS";

        [MaxLength(30)]
        [Display(Name = "الهاتف")]
        public string? Phone { get; set; }

        [MaxLength(200)]
        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        [MaxLength(260)]
        public string? LogoPath { get; set; }

        [MaxLength(300)]
        [Display(Name = "تذييل الفاتورة")]
        public string InvoiceFooter { get; set; } = "شكراً لتعاملكم معنا — Sama_POS";

        [Display(Name = "تفعيل الضريبة")]
        public bool TaxEnabled { get; set; } = true;

        [Display(Name = "النسبة العامة %")]
        [Column(TypeName = "decimal(18, 2)")]
        [Range(0, 100, ErrorMessage = "النسبة يجب أن تكون بين 0 و 100.")]
        public decimal DefaultTaxRate { get; set; }

        [Display(Name = "طباعة الإيصال بعد البيع")]
        public bool PrintReceiptAfterSale { get; set; }

        [Display(Name = "نقدي")]
        public bool EnablePaymentCash { get; set; } = true;

        [Display(Name = "بطاقة")]
        public bool EnablePaymentCard { get; set; } = true;

        [Display(Name = "تحويل")]
        public bool EnablePaymentTransfer { get; set; } = true;

        [BindNever]
        [MaxLength(100)]
        public string? FactoryResetPinHash { get; set; }
    }
}
