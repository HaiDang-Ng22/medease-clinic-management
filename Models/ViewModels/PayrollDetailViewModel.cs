namespace Healthy_System.Models.ViewModels
{
    public class PayrollDetailViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        public int Month { get; set; }
        public int Year { get; set; }

        public int TotalExams { get; set; }
        public int TotalShifts { get; set; }

        public decimal BaseSalary { get; set; }
        public decimal PerformanceSalary { get; set; }
        public decimal Allowance { get; set; }
        public decimal Deductions { get; set; }
        public decimal NetSalary { get; set; }
    }
}