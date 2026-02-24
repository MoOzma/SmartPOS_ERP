namespace SmartPOS_ERP.Models
{
    public class PurchaseInvoice
    {
        public int Id { get; set; }
        public DateTime InvoiceDate { get; set; }
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; }
        public List<PurchaseDetail> Details { get; set; } = new List<PurchaseDetail>();
    }
}
