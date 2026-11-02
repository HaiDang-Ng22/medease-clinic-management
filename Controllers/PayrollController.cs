using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Healthy_System.Data;
using Healthy_System.Models;
using Healthy_System.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Healthy_System.Controllers
{
    [Authorize(Roles = "Admin,Accountant")]
    public class PayrollController : Controller
    {
        private readonly AppDbContext _context;

        public PayrollController(AppDbContext context)
        {
            _context = context;
        }

        // --- Action 1: Bảng tổng hợp & Tính lương theo tháng ---
        public async Task<IActionResult> Index(int? month, int? year)
        {
            int selectedMonth = month ?? DateTime.Now.Month;
            int selectedYear = year ?? DateTime.Now.Year;

            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;

            // Lấy danh sách nhân sự (Bác sĩ, Lễ tân, KTV, Kế toán)
            var staffRoles = new[] { "Doctor", "Receptionist", "LabTech", "Accountant" };
            var staffUsers = await _context.Users
                .Where(u => staffRoles.Contains(u.Role))
                .ToListAsync();

            var configs = await _context.PayrollConfigs.ToDictionaryAsync(c => c.UserId);
            var payrollList = new List<PayrollDetailViewModel>();

            DateTime startDate = new DateTime(selectedYear, selectedMonth, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime endDate = startDate.AddMonths(1).AddDays(-1);

            foreach (var staff in staffUsers)
            {
                // Lấy cấu hình lương (nếu chưa cấu hình thì gán mặc định)
                var config = configs.ContainsKey(staff.Id) ? configs[staff.Id] : new PayrollConfig
                {
                    UserId = staff.Id,
                    BaseSalary = staff.Role == "Doctor" ? 12000000 : 7000000,
                    ShiftAllowance = 200000,
                    CommissionRate = staff.Role == "Doctor" ? 15.0 : 0.0
                };

                int totalExams = 0;
                decimal performanceSalary = 0;

                // Nếu là bác sĩ -> Tự động tổng hợp số ca khám & hoa hồng
                if (staff.Role == "Doctor")
                {
                    var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == staff.Id);
                    if (doctor != null)
                    {
                        var completedExams = await _context.Appointments
                            .Where(a => a.DoctorId == doctor.Id &&
                                        a.Status == "Completed" &&
                                        a.AppointmentDate >= startDate &&
                                        a.AppointmentDate <= endDate)
                            .ToListAsync();

                        totalExams = completedExams.Count;
                        decimal totalFee = completedExams.Sum(a => doctor.ConsultationFee);
                        performanceSalary = totalFee * (decimal)(config.CommissionRate / 100.0);
                    }
                }

                // Tự động tổng hợp số ca trực trong tháng từ lịch làm việc
                int totalShifts = await _context.DoctorSchedules
                    .CountAsync(s => s.Doctor!.UserId == staff.Id &&
                                     s.WorkDate >= startDate &&
                                     s.WorkDate <= endDate);

                decimal allowance = totalShifts * config.ShiftAllowance;
                decimal deductions = 0; // Có thể mở rộng khấu trừ thuế/bảo hiểm
                decimal netSalary = config.BaseSalary + performanceSalary + allowance - deductions;

                payrollList.Add(new PayrollDetailViewModel
                {
                    UserId = staff.Id,
                    FullName = staff.FullName,
                    Email = staff.Email,
                    Role = staff.Role,
                    Month = selectedMonth,
                    Year = selectedYear,
                    TotalExams = totalExams,
                    TotalShifts = totalShifts,
                    BaseSalary = config.BaseSalary,
                    PerformanceSalary = performanceSalary,
                    Allowance = allowance,
                    Deductions = deductions,
                    NetSalary = netSalary
                });
            }

            return View(payrollList);
        }

        // --- Action 2: Màn hình Cấu hình mức lương ---
        [HttpGet]
        public async Task<IActionResult> Config()
        {
            var staffRoles = new[] { "Doctor", "Receptionist", "LabTech", "Accountant" };
            var staffUsers = await _context.Users
                .Where(u => staffRoles.Contains(u.Role))
                .ToListAsync();

            var configs = await _context.PayrollConfigs.ToDictionaryAsync(c => c.UserId);
            var modelList = new List<PayrollConfigViewModel>();

            foreach (var user in staffUsers)
            {
                var cfg = configs.ContainsKey(user.Id) ? configs[user.Id] : null;
                modelList.Add(new PayrollConfigViewModel
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Role = user.Role,
                    BaseSalary = cfg?.BaseSalary ?? (user.Role == "Doctor" ? 12000000 : 7000000),
                    ShiftAllowance = cfg?.ShiftAllowance ?? 200000,
                    CommissionRate = cfg?.CommissionRate ?? (user.Role == "Doctor" ? 15.0 : 0.0)
                });
            }

            return View(modelList);
        }

        // --- Action 3: Lưu cấu hình lương ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveConfig(List<PayrollConfigViewModel> models)
        {
            foreach (var item in models)
            {
                var cfg = await _context.PayrollConfigs.FirstOrDefaultAsync(c => c.UserId == item.UserId);
                if (cfg == null)
                {
                    _context.PayrollConfigs.Add(new PayrollConfig
                    {
                        UserId = item.UserId,
                        BaseSalary = item.BaseSalary,
                        ShiftAllowance = item.ShiftAllowance,
                        CommissionRate = item.CommissionRate,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    cfg.BaseSalary = item.BaseSalary;
                    cfg.ShiftAllowance = item.ShiftAllowance;
                    cfg.CommissionRate = item.CommissionRate;
                    cfg.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã cập nhật cấu hình mức lương nhân sự thành công!";
            return RedirectToAction(nameof(Config));
        }

        // --- Action 4: In phiếu lương cá nhân (A4) ---
        public async Task<IActionResult> Detail(int userId, int month, int year)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            var config = await _context.PayrollConfigs.FirstOrDefaultAsync(c => c.UserId == userId) ?? new PayrollConfig();

            DateTime startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime endDate = startDate.AddMonths(1).AddDays(-1);

            int totalExams = 0;
            decimal performanceSalary = 0;

            if (doctor != null)
            {
                var completedExams = await _context.Appointments
                    .Where(a => a.DoctorId == doctor.Id && a.Status == "Completed" && a.AppointmentDate >= startDate && a.AppointmentDate <= endDate)
                    .ToListAsync();

                totalExams = completedExams.Count;
                performanceSalary = completedExams.Sum(a => doctor.ConsultationFee) * (decimal)(config.CommissionRate / 100.0);
            }

            int totalShifts = await _context.DoctorSchedules
                .CountAsync(s => s.Doctor!.UserId == userId && s.WorkDate >= startDate && s.WorkDate <= endDate);

            decimal allowance = totalShifts * config.ShiftAllowance;
            decimal deductions = 0;
            decimal netSalary = config.BaseSalary + performanceSalary + allowance - deductions;

            var vm = new PayrollDetailViewModel
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                Title = doctor?.Title ?? "Nhân viên",
                Month = month,
                Year = year,
                TotalExams = totalExams,
                TotalShifts = totalShifts,
                BaseSalary = config.BaseSalary,
                PerformanceSalary = performanceSalary,
                Allowance = allowance,
                Deductions = deductions,
                NetSalary = netSalary
            };

            return View(vm);
        }

        // --- Action 5: Xuất bảng lương ra file Excel (.csv) ---
        public async Task<IActionResult> ExportExcel(int month, int year)
        {
            var result = await Index(month, year) as ViewResult;
            var list = result?.Model as List<PayrollDetailViewModel> ?? new List<PayrollDetailViewModel>();

            var builder = new StringBuilder();
            builder.AppendLine("STT,Ho Ten,Vai Tro,So Ca Kham,So Ca Truc,Luong Cung (VND),Luong Hieu Suat (VND),Phu Cap (VND),Khau Tru (VND),Thuc Nhan (VND)");

            int index = 1;
            foreach (var item in list)
            {
                builder.AppendLine($"{index++},{item.FullName},{item.Role},{item.TotalExams},{item.TotalShifts},{item.BaseSalary},{item.PerformanceSalary},{item.Allowance},{item.Deductions},{item.NetSalary}");
            }

            byte[] buffer = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
            return File(buffer, "text/csv", $"BangLuong_Thang_{month}_{year}.csv");
        }
    }
}