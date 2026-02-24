namespace SmartPOS_ERP.Models
{
    public class Expense
    {
        public int Id { get; set; }
        public string Description { get; set; } // وصف المصروف (مثلاً: إيجار شهر فبراير)
        public decimal Amount { get; set; }      // المبلغ
        public DateTime ExpenseDate { get; set; } // تاريخ الصرف
        public string Category { get; set; }    // الفئة (إيجار، كهرباء، أدوات تشغيل)
    }
}