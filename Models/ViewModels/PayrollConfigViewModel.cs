using System.Collections.Generic;

namespace Healthy_System.Models.ViewModels
{
    public class PayrollConfigViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public decimal BaseSalary { get; set; }
        public decimal ShiftAllowance { get; set; }
        public double CommissionRate { get; set; }
    }
}