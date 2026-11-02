using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Healthy_System.Models
{
    public class PayrollConfig
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseSalary { get; set; } = 5000000; // Lương cơ bản

        [Column(TypeName = "decimal(18,2)")]
        public decimal ShiftAllowance { get; set; } = 200000; // Phụ cấp mỗi ca trực

        public double CommissionRate { get; set; } = 10.0; // % Hoa hồng/ca khám (ví dụ 10%)

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}