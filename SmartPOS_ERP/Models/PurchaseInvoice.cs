namespace SmartPOS_ERP.Models
{
    public class PurchaseInvoice
    {
        public int Id { get; set; }
        public DateTime InvoiceDate { get; set; }
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; }
        public bool IsVoided { get; set; }
        public DateTime? VoidedAt { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(64)]
        public string? VoidedBy { get; set; }

        public List<PurchaseDetail> Details { get; set; } = new List<PurchaseDetail>();
        public List<PurchaseReturn> Returns { get; set; } = [];
    }
}
