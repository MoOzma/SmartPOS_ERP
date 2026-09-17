using System.ComponentModel.DataAnnotations;
using SmartPOS_ERP.Security;

namespace SmartPOS_ERP.Models
{
    public class User
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(64)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم الظاهر مطلوب")]
        [MaxLength(80)]
        [Display(Name = "الاسم الظاهر")]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(30)]
        [Display(Name = "الهاتف")]
        public string? Phone { get; set; }

        [Required]
        [StrongPassword]
        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = "Cashier";

        [Display(Name = "الحساب مفعّل")]
        public bool IsActive { get; set; } = true;

        public bool CanProcessReturn { get; set; }
        public bool CanManageProducts { get; set; }
        public bool CanViewAllOrders { get; set; }
        public bool CanManageExpenses { get; set; }
        public bool CanManagePurchases { get; set; }
        public bool CanViewDashboard { get; set; }
    }
}