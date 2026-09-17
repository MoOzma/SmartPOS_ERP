using System.ComponentModel.DataAnnotations;

namespace SmartPOS_ERP.Models
{
    public class Expense
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الوصف مطلوب.")]
        public string Description { get; set; }

        [Range(typeof(decimal), "0.01", "999999999", ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر.")]
        public decimal Amount { get; set; }

        public DateTime ExpenseDate { get; set; }

        [Required(ErrorMessage = "نوع المصروف مطلوب.")]
        public string Category { get; set; }
        public bool FromCash { get; set; } = true;
        public int? ShiftId { get; set; }
        public Shift? Shift { get; set; }
    }
}