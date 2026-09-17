namespace SmartPOS_ERP.Models;

public class DashboardViewModel
{
    public decimal TodaySales { get; set; }
    public decimal MonthSales { get; set; }
    public decimal MonthProfit { get; set; }
    public decimal SupplierPayables { get; set; }
    public decimal MonthExpenses { get; set; }
    public int MonthInvoiceCount { get; set; }
    public int MonthReturnCount { get; set; }
    public decimal MonthReturnAmount { get; set; }
    public decimal StockValue { get; set; }
    public decimal CashBalance { get; set; }
    public int OpenShiftCount { get; set; }
    public int MonthPurchaseCount { get; set; }
    public int PrevMonthInvoiceCount { get; set; }
    public int PrevMonthPurchaseCount { get; set; }
    public decimal CustomerReceivables { get; set; }
    public int ExpiringCount { get; set; }
    public int LowStockCount => LowStock.Count;
    public decimal YearSales => MonthlySales.Sum(x => x.Total);
    public decimal YearExpenses => MonthlySales.Sum(x => x.Expenses);
    public decimal YearPurchases => MonthlySales.Sum(x => x.Purchases);
    public decimal YearOperatingNet => YearSales - YearExpenses;
    public List<DashboardMonthPoint> MonthlySales { get; set; } = [];
    public List<DashboardNamedValue> TopProducts { get; set; } = [];
    public List<DashboardHourPoint> HourlySales { get; set; } = [];
    public List<DashboardInvoiceRow> RecentInvoices { get; set; } = [];
    public List<DashboardLowStockRow> LowStock { get; set; } = [];
}

public class DashboardMonthPoint
{
    public string Label { get; set; } = "";
    public decimal Total { get; set; }
    public decimal Expenses { get; set; }
    public decimal Purchases { get; set; }
    public int Count { get; set; }
}

public class DashboardNamedValue
{
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Sales { get; set; }
}

public class DashboardHourPoint
{
    public int Hour { get; set; }
    public decimal Total { get; set; }
    public int Count { get; set; }
}

public class DashboardInvoiceRow
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Total { get; set; }
}

public class DashboardLowStockRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Stock { get; set; }
    public int ReorderLevel { get; set; }
    public string Unit { get; set; } = "";
}
