namespace SmartPOS_ERP.Models;

public class SupplierDirectoryViewModel
{
    public string? Query { get; set; }
    public string Status { get; set; } = "all";
    public int SupplierCount { get; set; }
    public int DueCount { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalBalance { get; set; }
    public List<SupplierDirectoryRow> Items { get; set; } = [];
}

public class SupplierDirectoryRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public int InvoiceCount { get; set; }
    public DateTime? LastInvoiceDate { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal Balance { get; set; }
}
