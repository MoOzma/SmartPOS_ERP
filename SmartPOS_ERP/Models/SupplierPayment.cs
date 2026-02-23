using SmartPOS_ERP.Models;

public class SupplierPayment
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public decimal AmountPaid { get; set; } // المبلغ المدفوع
    public DateTime PaymentDate { get; set; }
    public string? Notes { get; set; } // ملاحظات (مثلاً: دفع نقدي، تحويل بنكي)

    public Supplier Supplier { get; set; }
}