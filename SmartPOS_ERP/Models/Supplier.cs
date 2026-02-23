using System.ComponentModel.DataAnnotations;

namespace SmartPOS_ERP.Models
{
    public class Supplier
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المورد مطلوب")]
        [Display(Name = "اسم المورد/الشركة")]
        public string Name { get; set; }

        [Display(Name = "رقم الهاتف")]
        public string? Phone { get; set; }

        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        // ربط المورد بفواتير الشراء التي سننشئها لاحقاً
        public virtual ICollection<PurchaseInvoice>? PurchaseInvoices { get; set; }

        public List<PurchaseInvoice> Invoices { get; set; }
        public List<SupplierPayment> Payments { get; set; }

        // إجمالي قيمة المشتريات من هذا المورد
        public decimal TotalPurchases => Invoices?.Sum(i => i.Details.Sum(d => d.PackageQuantity * d.PackageCost)) ?? 0;

        // إجمالي ما تم دفعه فعلياً
        public decimal TotalPaid => Payments?.Sum(p => p.AmountPaid) ?? 0;

        // الرصيد المتبقي (الديون)
        public decimal Balance => TotalPurchases - TotalPaid;


    }
}