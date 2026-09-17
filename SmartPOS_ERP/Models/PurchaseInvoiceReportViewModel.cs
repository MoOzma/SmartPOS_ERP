namespace SmartPOS_ERP.Models;

public class PurchaseInvoiceReportViewModel
{
    public string PeriodKind { get; set; } = "month";
    public DateTime SelectedDate { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int SelectedMonth { get; set; }
    public int SelectedYear { get; set; }
    public string PeriodLabel { get; set; } = "";
    public string? Query { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalUnits { get; set; }
    public int InvoiceCount { get; set; }
    public int SupplierCount { get; set; }
    public string TopSupplier { get; set; } = "";
    public List<PurchaseInvoiceReportRow> Items { get; set; } = [];
    public List<PurchaseSupplierRow> Suppliers { get; set; } = [];
}

public class PurchaseInvoiceReportRow
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public int LineCount { get; set; }
    public decimal TotalUnits { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsVoided { get; set; }
}

public class PurchaseSupplierRow
{
    public int SupplierId { get; set; }
    public string Name { get; set; } = "";
    public int InvoiceCount { get; set; }
    public decimal Amount { get; set; }
    public decimal Percent { get; set; }
}
