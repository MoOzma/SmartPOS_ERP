namespace SmartPOS_ERP.Models
{
    public class PurchaseDetail
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int PurchaseInvoiceId { get; set; }

        public PurchaseInvoice PurchaseInvoice { get; set; } 
        public decimal PackageQuantity { get; set; } // الكمية بالعلبة (مثلاً: 4)
        public decimal UnitsPerPackage { get; set; }  // معامل التحويل (مثلاً: 12)
        public decimal TotalUnits { get; set; }       // الإجمالي (48) - يتم حسابه تلقائياً
        public decimal PackageCost { get; set; }      // سعر شراء العلبة الواحدة
        public decimal UnitCost { get; set; }         // سعر تكلفة القطعة الواحدة (للمخزن)
        public Product Product { get; set; }
    }
}
