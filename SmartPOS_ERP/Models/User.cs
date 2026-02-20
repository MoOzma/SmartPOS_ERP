using System.ComponentModel.DataAnnotations;

namespace SmartPOS_ERP.Models
{
    public class User
    {
        public int Id { get; set; }
        [Required]
        public string Username { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Cashier";  
    }
}