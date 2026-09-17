namespace SmartPOS_ERP.Models;

public class SalesProfitReportViewModel
{
    public string PeriodKind { get; set; } = "day";
    public DateTime SelectedDate { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int SelectedMonth { get; set; }
    public int SelectedYear { get; set; }
    public string PeriodLabel { get; set; } = "";
    public decimal GrossSales { get; set; }
    public decimal Returns { get; set; }
    public decimal NetSales { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetProfit { get; set; }
    public int InvoiceCount { get; set; }
    public int ReturnCount { get; set; }
    public List<DashboardInvoiceRow> Invoices { get; set; } = [];
}
