using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartPOS_ERP.Models
{
    public class Shift
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTime OpenedAt { get; set; } = DateTime.Now;
        public DateTime? ClosedAt { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "عهدة أول المدة")]
        [Range(0, double.MaxValue)]
        public decimal OpeningCash { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "العدد الفعلي")]
        [Range(0, double.MaxValue)]
        public decimal? ClosingCash { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "المتوقع")]
        public decimal? ExpectedCash { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "العجز / الزيادة")]
        public decimal? Difference { get; set; }

        [MaxLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [NotMapped]
        public bool IsOpen => ClosedAt == null;
    }
}
