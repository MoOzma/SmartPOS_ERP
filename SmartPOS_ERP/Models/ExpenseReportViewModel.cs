namespace SmartPOS_ERP.Models;

public class ExpenseReportViewModel
{
    public string PeriodKind { get; set; } = "month";
    public DateTime SelectedDate { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int SelectedMonth { get; set; }
    public int SelectedYear { get; set; }
    public string PeriodLabel { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public int Count { get; set; }
    public decimal AverageAmount { get; set; }
    public string TopCategory { get; set; } = "";
    public List<Expense> Items { get; set; } = [];
    public List<ExpenseCategoryRow> Categories { get; set; } = [];
}

public class ExpenseCategoryRow
{
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Percent { get; set; }
}
