using System.ComponentModel.DataAnnotations;

namespace SmartPOS_ERP.Models
{
    public class StaffFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المستخدم مطلوب")]
        [MaxLength(64)]
        [Display(Name = "اسم المستخدم")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم الظاهر مطلوب")]
        [MaxLength(80)]
        [Display(Name = "الاسم الظاهر")]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(30)]
        [Display(Name = "الهاتف")]
        public string? Phone { get; set; }

        [Display(Name = "الرقم السري")]
        public string? Password { get; set; }

        [Required]
        public string Role { get; set; } = "Cashier";

        public bool IsActive { get; set; } = true;

        public bool CanProcessReturn { get; set; }
        public bool CanManageProducts { get; set; }
        public bool CanViewAllOrders { get; set; }
        public bool CanManageExpenses { get; set; }
        public bool CanManagePurchases { get; set; }
        public bool CanViewDashboard { get; set; }

        public bool IsEdit { get; set; }
    }
}
