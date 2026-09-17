using System.ComponentModel.DataAnnotations;

namespace SmartPOS_ERP.Models;

public class Customer
{
    public int Id { get; set; }

    [Required(ErrorMessage = "اسم العميل مطلوب")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Notes { get; set; }

    public List<CreditInvoice> Invoices { get; set; } = [];
}
