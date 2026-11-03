using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Healthy_System.Models
{
    public class Payroll
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        public int Month { get; set; }
        public int Year { get; set; }

        public int TotalExams { get; set; } // Số ca khám trong tháng
        public int TotalShifts { get; set; } // Số ca trực trong tháng

        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseSalary { get; set; } // Lương cứng

        [Column(TypeName = "decimal(18,2)")]
        public decimal PerformanceSalary { get; set; } // Lương hiệu suất (% hoa hồng khám)

        [Column(TypeName = "decimal(18,2)")]
        public decimal Allowance { get; set; } // Phụ cấp ca trực

        [Column(TypeName = "decimal(18,2)")]
        public decimal Deductions { get; set; } = 0; // Khấu trừ (BHYT/BHXH/Thuế/Phạt)

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetSalary { get; set; } // Lương thực nhận

        public string Status { get; set; } = "Calculated"; // Calculated / Approved / Paid
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}