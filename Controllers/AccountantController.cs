using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Healthy_System.Data;
using Healthy_System.Models;
using Healthy_System.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Healthy_System.Controllers
{
    [Authorize(Roles = "Accountant,Admin")]
    public class AccountantController : Controller
    {
        private readonly AppDbContext _context;

        public AccountantController(AppDbContext context)
        {
            _context = context;
        }

        private int? GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId)) return userId;
            return null;
        }

        // ==========================================
        // US31: Dashboard Kế toán tổng quan
        // ==========================================
        public IActionResult Dashboard()
        {
            var today = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);

            var completedAppointments = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .Where(a => a.Status == "Completed")
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.UpdatedAt)
                .Take(15)
                .ToList();

            var apptIds = completedAppointments.Select(a => a.Id).ToList();
            var costs = _context.PatientCosts
                .Where(c => apptIds.Contains(c.AppointmentId))
                .ToDictionary(c => c.AppointmentId);

            ViewBag.Costs = costs;

            // Thống kê
            var totalCompleted = _context.Appointments.Count(a => a.Status == "Completed");
            var totalCalculated = _context.PatientCosts.Count(c => c.PaymentStatus == "Calculated");
            var totalPaid = _context.PatientCosts.Count(c => c.PaymentStatus == "Paid");
            var totalRevenue = _context.PatientCosts.Where(c => c.PaymentStatus == "Paid").Sum(c => c.TotalAmount);

            ViewBag.TotalCompleted = totalCompleted;
            ViewBag.TotalCalculated = totalCalculated;
            ViewBag.TotalPaid = totalPaid;
            ViewBag.TotalRevenue = totalRevenue;

            return View(completedAppointments);
        }

        // ==========================================
        // US31: Danh sách chi phí bệnh nhân
        // ==========================================
        public IActionResult PatientCosts(string? search, string? status, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .Where(a => a.Status == "Completed")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim().ToLower();
                query = query.Where(a =>
                    (a.Patient != null && a.Patient.FullName.ToLower().Contains(kw)) ||
                    a.AppointmentCode.ToLower().Contains(kw) ||
                    (a.Patient != null && a.Patient.PhoneNumber != null && a.Patient.PhoneNumber.Contains(kw)));
            }

            if (fromDate.HasValue)
            {
                var fromUtc = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
                query = query.Where(a => a.AppointmentDate >= fromUtc);
            }

            if (toDate.HasValue)
            {
                var toUtc = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                query = query.Where(a => a.AppointmentDate <= toUtc);
            }

            var list = query
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.Id)
                .ToList();

            var apptIds = list.Select(a => a.Id).ToList();
            var costs = _context.PatientCosts
                .Where(c => apptIds.Contains(c.AppointmentId))
                .ToDictionary(c => c.AppointmentId);

            // Lọc theo trạng thái chi phí nếu có yêu cầu
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "Uncalculated")
                {
                    list = list.Where(a => !costs.ContainsKey(a.Id)).ToList();
                }
                else if (status == "Calculated")
                {
                    list = list.Where(a => costs.ContainsKey(a.Id) && costs[a.Id].PaymentStatus == "Calculated").ToList();
                }
                else if (status == "Paid")
                {
                    list = list.Where(a => costs.ContainsKey(a.Id) && costs[a.Id].PaymentStatus == "Paid").ToList();
                }
            }

            ViewBag.Costs = costs;
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(list);
        }

        // ==========================================
        // US31: Form tính toán chi phí khám bệnh (GET)
        // ==========================================
        [HttpGet]
        public IActionResult Calculate(int appointmentId)
        {
            var appt = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == appointmentId);

            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ca khám bệnh yêu cầu.";
                return RedirectToAction(nameof(PatientCosts));
            }

            if (appt.Status != "Completed")
            {
                TempData["ErrorMessage"] = "Chỉ có thể lập bảng tính chi phí cho ca khám đã hoàn thành chẩn đoán!";
                return RedirectToAction(nameof(PatientCosts));
            }

            // Kiểm tra kết quả xét nghiệm nếu có
            var labResult = _context.LabResults.FirstOrDefault(r => r.AppointmentId == appt.Id);

            // Kiểm tra xem đã có bảng chi phí lưu trước đó chưa
            var existingCost = _context.PatientCosts.FirstOrDefault(c => c.AppointmentId == appt.Id);

            var availableServices = _context.Services
                .Where(s => s.IsActive)
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Name)
                .Select(s => new ServiceItemDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Price = s.Price,
                    Category = s.Category
                })
                .ToList();

            var model = new CalculateCostViewModel
            {
                AppointmentId = appt.Id,
                AppointmentCode = appt.AppointmentCode,
                PatientName = appt.Patient?.FullName ?? "Chưa rõ",
                PatientPhone = appt.Patient?.PhoneNumber,
                PatientGender = appt.Patient?.Gender,
                PatientDob = appt.Patient?.DateOfBirth,
                DoctorName = $"{appt.Doctor?.Title} {appt.Doctor?.User?.FullName}".Trim(),
                SpecialtyName = appt.Specialty?.Name ?? string.Empty,
                AppointmentDate = appt.AppointmentDate,
                TimeSlot = appt.TimeSlot,
                Diagnosis = appt.Diagnosis,
                Prescription = appt.Prescription,
                LabRequest = appt.LabRequest,
                LabResultSummary = labResult?.ResultSummary,
                AvailableServices = availableServices
            };

            if (existingCost != null)
            {
                // Load dữ liệu đã lưu
                model.ConsultationFee = existingCost.ConsultationFee;
                model.ServiceFee = existingCost.ServiceFee;
                model.ServiceDetails = existingCost.ServiceDetails;
                model.LabFee = existingCost.LabFee;
                model.LabDetails = existingCost.LabDetails;
                model.PrescriptionFee = existingCost.PrescriptionFee;
                model.PrescriptionDetails = existingCost.PrescriptionDetails;
                model.HealthInsuranceCoveragePercent = existingCost.HealthInsuranceCoveragePercent;
                model.DiscountAmount = existingCost.DiscountAmount;
                model.TotalAmount = existingCost.TotalAmount;
                model.PaymentStatus = existingCost.PaymentStatus;
                model.PaymentMethod = existingCost.PaymentMethod ?? "Tiền mặt";
                model.Notes = existingCost.Notes;
            }
            else
            {
                // Khởi tạo mặc định
                model.ConsultationFee = appt.Doctor?.ConsultationFee ?? 200000;

                // Gợi ý nếu có chỉ định xét nghiệm
                if (!string.IsNullOrWhiteSpace(appt.LabRequest))
                {
                    model.LabFee = 150000;
                    model.LabDetails = $"Chỉ định bác sĩ: {appt.LabRequest}";
                }

                // Gợi ý nếu có kê đơn thuốc
                if (!string.IsNullOrWhiteSpace(appt.Prescription))
                {
                    model.PrescriptionFee = 120000;
                    model.PrescriptionDetails = appt.Prescription;
                }

                decimal subTotal = model.ConsultationFee + model.ServiceFee + model.LabFee + model.PrescriptionFee;
                model.DiscountAmount = Math.Round(subTotal * model.HealthInsuranceCoveragePercent / 100, 0);
                model.TotalAmount = Math.Max(0, subTotal - model.DiscountAmount);
            }

            return View(model);
        }

        // ==========================================
        // US31: Form tính toán chi phí khám bệnh (POST)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Calculate(CalculateCostViewModel model)
        {
            var appt = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefault(a => a.Id == model.AppointmentId);

            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ca khám.";
                return RedirectToAction(nameof(PatientCosts));
            }

            // Tính toán tổng tiền chính xác
            decimal subTotal = model.ConsultationFee + model.ServiceFee + model.LabFee + model.PrescriptionFee;
            if (model.HealthInsuranceCoveragePercent > 0)
            {
                model.DiscountAmount = Math.Round(subTotal * model.HealthInsuranceCoveragePercent / 100, 0);
            }
            model.TotalAmount = Math.Max(0, subTotal - model.DiscountAmount);

            if (!ModelState.IsValid)
            {
                model.AvailableServices = _context.Services
                    .Where(s => s.IsActive)
                    .Select(s => new ServiceItemDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Price = s.Price,
                        Category = s.Category
                    }).ToList();
                return View(model);
            }

            var accountantId = GetCurrentUserId();
            var existingCost = _context.PatientCosts.FirstOrDefault(c => c.AppointmentId == appt.Id);

            if (existingCost == null)
            {
                existingCost = new PatientCost
                {
                    AppointmentId = appt.Id,
                    AccountantId = accountantId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.PatientCosts.Add(existingCost);
            }
            else
            {
                existingCost.AccountantId = accountantId;
            }

            existingCost.ConsultationFee = model.ConsultationFee;
            existingCost.ServiceFee = model.ServiceFee;
            existingCost.ServiceDetails = model.ServiceDetails?.Trim();
            existingCost.LabFee = model.LabFee;
            existingCost.LabDetails = model.LabDetails?.Trim();
            existingCost.PrescriptionFee = model.PrescriptionFee;
            existingCost.PrescriptionDetails = model.PrescriptionDetails?.Trim();
            existingCost.HealthInsuranceCoveragePercent = model.HealthInsuranceCoveragePercent;
            existingCost.DiscountAmount = model.DiscountAmount;
            existingCost.TotalAmount = model.TotalAmount;
            existingCost.PaymentStatus = model.PaymentStatus;
            existingCost.PaymentMethod = model.PaymentMethod;
            existingCost.Notes = model.Notes?.Trim();

            if (model.PaymentStatus == "Paid" && !existingCost.PaidAt.HasValue)
            {
                existingCost.PaidAt = DateTime.UtcNow;
            }

            // Gửi thông báo đến bệnh nhân nếu được chọn
            if (model.NotifyPatient && appt.PatientId > 0)
            {
                string statusText = model.PaymentStatus == "Paid" ? "Đã thanh toán hoàn tất" : "Chờ thanh toán tại quầy";
                _context.Notifications.Add(new Notification
                {
                    UserId = appt.PatientId,
                    Title = "Thông báo bảng chi phí khám bệnh",
                    Message = $"Bảng chi phí ca khám {appt.AppointmentCode} đã được bộ phận Kế toán lập: Tổng chi phí thanh toán là {model.TotalAmount:N0} VNĐ. Trạng thái: {statusText}.",
                    Type = "StatusUpdate",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã lưu bảng tính chi phí cho bệnh nhân {appt.Patient?.FullName} ({appt.AppointmentCode}) thành công!";
            return RedirectToAction(nameof(CostSheet), new { id = existingCost.Id });
        }

        // ==========================================
        // US31: Bảng kê chi phí khám bệnh (Chi tiết / In / PDF)
        // ==========================================
        [HttpGet]
        [Authorize] // Cho phép Bệnh nhân, Bác sĩ, Tiếp tân, Kế toán cùng xem bảng chi phí minh bạch
        public IActionResult CostSheet(int id)
        {
            var cost = _context.PatientCosts
                .Include(c => c.Appointment).ThenInclude(a => a.Patient)
                .Include(c => c.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
                .Include(c => c.Appointment).ThenInclude(a => a.Specialty)
                .Include(c => c.Accountant)
                .FirstOrDefault(c => c.Id == id);

            if (cost == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bảng kê chi phí.";
                return RedirectToAction(nameof(PatientCosts));
            }

            return View(cost);
        }

        // ==========================================
        // Xác nhận thanh toán nhanh
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAsPaid(int id, string paymentMethod)
        {
            var cost = _context.PatientCosts
                .Include(c => c.Appointment).ThenInclude(a => a.Patient)
                .FirstOrDefault(c => c.Id == id);

            if (cost == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bảng kê chi phí.";
                return RedirectToAction(nameof(PatientCosts));
            }

            cost.PaymentStatus = "Paid";
            cost.PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Tiền mặt" : paymentMethod;
            cost.PaidAt = DateTime.UtcNow;

            if (cost.Appointment?.PatientId > 0)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = cost.Appointment.PatientId,
                    Title = "Xác nhận đã thanh toán viện phí",
                    Message = $"Ca khám {cost.Appointment.AppointmentCode} đã được xác nhận thanh toán {cost.TotalAmount:N0} VNĐ bằng phương thức {cost.PaymentMethod}.",
                    Type = "StatusUpdate",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _context.SaveChanges();
            TempData["SuccessMessage"] = $"Đã xác nhận thanh toán thành công cho ca khám {cost.Appointment?.AppointmentCode}!";
            return RedirectToAction(nameof(CostSheet), new { id = cost.Id });
        }
    }
}
