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
            var today = DateTime.Today;

            var completedAppointments = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)!.ThenInclude(d => d.User)
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
            var totalPaid = _context.Appointments.Count(a => a.PaymentStatus == "Paid");
            var totalRevenue = _context.Appointments.Where(a => a.PaymentStatus == "Paid").Sum(a => a.PaidAmount);
            if (totalRevenue == 0m)
            {
                totalRevenue = _context.PatientCosts.Where(c => c.PaymentStatus == "Paid").Sum(c => c.TotalAmount);
            }

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
                .Include(a => a.Doctor)!.ThenInclude(d => d.User)
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

            var appointments = query
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.UpdatedAt)
                .ToList();

            var apptIds = appointments.Select(a => a.Id).ToList();
            var costs = _context.PatientCosts
                .Where(c => apptIds.Contains(c.AppointmentId))
                .ToDictionary(c => c.AppointmentId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "Uncalculated" || status == "NotCalculated")
                {
                    appointments = appointments.Where(a => !costs.ContainsKey(a.Id)).ToList();
                }
                else
                {
                    appointments = appointments.Where(a => costs.ContainsKey(a.Id) && costs[a.Id].PaymentStatus == status).ToList();
                }
            }

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.Costs = costs;

            return View(appointments);
        }

        // ==========================================
        // US31: Form Tính toán chi phí khám bệnh
        // ==========================================
        [HttpGet]
        public IActionResult Calculate(int id, int? appointmentId = null)
        {
            int targetId = id > 0 ? id : (appointmentId ?? 0);
            var appt = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)!.ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == targetId);

            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ ca khám.";
                return RedirectToAction(nameof(PatientCosts));
            }

            var existingCost = _context.PatientCosts.FirstOrDefault(c => c.AppointmentId == targetId);

            // Bóc tách tiền khám
            decimal consultationFee = existingCost?.ConsultationFee ?? (appt.Doctor?.ConsultationFee ?? 200000m);

            // Bóc tách dịch vụ kỹ thuật
            decimal serviceFee = existingCost?.ServiceFee ?? 0m;
            string serviceDetails = existingCost?.ServiceDetails ?? "";

            var availableServices = _context.Services.Select(s => new ServiceItemDto
            {
                Id = s.Id,
                Name = s.Name,
                Price = s.Price,
                Category = s.Category ?? "Dịch vụ chung"
            }).ToList();

            // Bóc tách xét nghiệm cận lâm sàng
            decimal labFee = existingCost?.LabFee ?? 0m;
            string labDetails = existingCost?.LabDetails ?? "";

            if (existingCost == null)
            {
                var labResults = _context.LabResults.Where(r => r.AppointmentId == targetId).ToList();
                var labItems = new List<string>();

                if (labResults.Any())
                {
                    foreach (var lr in labResults)
                    {
                        decimal fee = 120000m;
                        var summary = lr.ResultSummary ?? "Xét nghiệm cận lâm sàng";
                        if (summary.Contains("Máu") || summary.Contains("Huyết học")) fee = 100000m;
                        else if (summary.Contains("Sinh hóa")) fee = 150000m;
                        else if (summary.Contains("Siêu âm")) fee = 200000m;
                        else if (summary.Contains("X-quang") || summary.Contains("X Quang")) fee = 180000m;
                        else if (summary.Contains("Nước tiểu")) fee = 80000m;

                        labFee += fee;
                        labItems.Add($"{summary} ({fee:N0} đ)");
                    }
                }
                else if (!string.IsNullOrWhiteSpace(appt.LabRequest))
                {
                    var requests = appt.LabRequest.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                                 .Select(r => r.Trim()).ToList();
                    foreach (var req in requests)
                    {
                        decimal fee = 120000m;
                        labFee += fee;
                        labItems.Add($"{req} ({fee:N0} đ)");
                    }
                }
                labDetails = string.Join("; ", labItems);
            }

            // Bóc tách đơn thuốc
            decimal prescriptionFee = existingCost?.PrescriptionFee ?? 0m;
            string prescriptionDetails = existingCost?.PrescriptionDetails ?? "";

            if (existingCost == null)
            {
                if (!string.IsNullOrWhiteSpace(appt.Prescription))
                {
                    prescriptionFee = 150000m;
                    prescriptionDetails = appt.Prescription.Trim();
                }
            }

            int insurancePercent = existingCost?.HealthInsuranceCoveragePercent ?? 0;
            decimal subTotal = consultationFee + serviceFee + labFee + prescriptionFee;
            decimal discount = existingCost?.DiscountAmount ?? (subTotal * insurancePercent / 100m);
            decimal total = existingCost?.TotalAmount ?? Math.Max(0, subTotal - discount);

            var vm = new CalculateCostViewModel
            {
                AppointmentId = appt.Id,
                AppointmentCode = appt.AppointmentCode,
                PatientName = appt.Patient?.FullName ?? "Chưa có tên",
                PatientPhone = appt.Patient?.PhoneNumber ?? "—",
                PatientGender = appt.Patient?.Gender ?? "—",
                PatientDob = appt.Patient?.DateOfBirth,
                DoctorName = $"{appt.Doctor?.Title} {appt.Doctor?.User?.FullName}".Trim(),
                SpecialtyName = appt.Specialty?.Name ?? "—",
                AppointmentDate = appt.AppointmentDate,
                TimeSlot = appt.TimeSlot,
                Diagnosis = appt.Diagnosis ?? "Chưa ghi nhận chẩn đoán",
                Prescription = appt.Prescription,
                LabRequest = appt.LabRequest,
                LabResultSummary = _context.LabResults.FirstOrDefault(r => r.AppointmentId == targetId)?.ResultSummary,
                ConsultationFee = consultationFee,
                ServiceFee = serviceFee,
                ServiceDetails = serviceDetails,
                LabFee = labFee,
                LabDetails = labDetails,
                PrescriptionFee = prescriptionFee,
                PrescriptionDetails = prescriptionDetails,
                HealthInsuranceCoveragePercent = insurancePercent,
                DiscountAmount = discount,
                TotalAmount = total,
                PaymentStatus = existingCost?.PaymentStatus ?? (appt.PaymentStatus ?? "Calculated"),
                PaymentMethod = existingCost?.PaymentMethod ?? (appt.PaymentMethod ?? "Tiền mặt"),
                Notes = existingCost?.Notes,
                AvailableServices = availableServices
            };

            return View(vm);
        }

        // ==========================================
        // US31: Lưu Bảng tính toán chi phí khám bệnh
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Calculate(CalculateCostViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu tính phí không hợp lệ. Vui lòng kiểm tra lại.";
                return View(model);
            }

            var appt = _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefault(a => a.Id == model.AppointmentId);

            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ ca khám.";
                return RedirectToAction(nameof(PatientCosts));
            }

            var currentUserId = GetCurrentUserId();
            var existingCost = _context.PatientCosts.FirstOrDefault(c => c.AppointmentId == model.AppointmentId);

            if (existingCost == null)
            {
                existingCost = new PatientCost
                {
                    AppointmentId = model.AppointmentId,
                    AccountantId = currentUserId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.PatientCosts.Add(existingCost);
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

            // Đồng bộ trạng thái thanh toán sang Appointment
            appt.PaidAmount = model.TotalAmount;
            if (model.PaymentStatus == "Paid")
            {
                appt.PaymentStatus = "Paid";
                appt.PaymentMethod = model.PaymentMethod;
                appt.PaidAt ??= DateTime.UtcNow;
                existingCost.PaidAt ??= DateTime.UtcNow;
                if (string.IsNullOrEmpty(appt.InvoiceCode))
                {
                    appt.InvoiceCode = $"HD-{DateTime.Now:yyyyMMdd}-{appt.Id:D4}";
                }
            }
            else
            {
                appt.PaymentStatus = "Unpaid";
            }

            // Gửi thông báo đến bệnh nhân nếu được chọn
            if (model.NotifyPatient && appt.PatientId > 0)
            {
                string statusText = model.PaymentStatus == "Paid" ? "Đã thanh toán hoàn tất" : "Chờ thanh toán tại quầy";
                _context.Notifications.Add(new Notification
                {
                    UserId = appt.PatientId,
                    Title = "Bảng kê chi phí khám bệnh",
                    Message = $"Bảng kê chi phí ca khám {appt.AppointmentCode} đã được lập: Tổng cộng {model.TotalAmount:N0} VNĐ ({statusText}).",
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
        [Authorize]
        public IActionResult CostSheet(int id)
        {
            var cost = _context.PatientCosts
                .Include(c => c.Appointment)!.ThenInclude(a => a.Patient)
                .Include(c => c.Appointment)!.ThenInclude(a => a.Doctor)!.ThenInclude(d => d.User)
                .Include(c => c.Appointment)!.ThenInclude(a => a.Specialty)
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
        // US32: Danh sách Hóa đơn & Phiếu thu thanh toán
        // ==========================================
        public IActionResult Invoices(string? search, string? status, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)!.ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .Where(a => a.Status == "Completed" || a.PaymentStatus == "Paid")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim().ToLower();
                query = query.Where(a =>
                    (a.Patient != null && a.Patient.FullName.ToLower().Contains(kw)) ||
                    a.AppointmentCode.ToLower().Contains(kw) ||
                    (a.InvoiceCode != null && a.InvoiceCode.ToLower().Contains(kw)) ||
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

            var appts = query.OrderByDescending(a => a.AppointmentDate).ToList();

            var apptIds = appts.Select(a => a.Id).ToList();
            var costs = _context.PatientCosts
                .Where(c => apptIds.Contains(c.AppointmentId))
                .ToDictionary(c => c.AppointmentId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "Paid")
                {
                    appts = appts.Where(a => a.PaymentStatus == "Paid" || (costs.ContainsKey(a.Id) && costs[a.Id].PaymentStatus == "Paid")).ToList();
                }
                else if (status == "Unpaid")
                {
                    appts = appts.Where(a => a.PaymentStatus != "Paid" && (!costs.ContainsKey(a.Id) || costs[a.Id].PaymentStatus != "Paid")).ToList();
                }
            }

            ViewBag.Costs = costs;
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.TotalPaidCount = _context.Appointments.Count(a => a.PaymentStatus == "Paid");
            ViewBag.TotalUnpaidCount = appts.Count(a => a.PaymentStatus != "Paid");

            return View(appts);
        }

        // ==========================================
        // US32: Giao diện thu tiền & xuất hóa đơn / phiếu thu
        // ==========================================
        [HttpGet]
        public IActionResult ProcessPayment(int id)
        {
            var appt = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)!.ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == id);

            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ ca khám.";
                return RedirectToAction(nameof(Invoices));
            }

            var cost = _context.PatientCosts.FirstOrDefault(c => c.AppointmentId == id);

            decimal totalAmount = 0m;
            if (cost != null)
            {
                totalAmount = cost.TotalAmount;
            }
            else
            {
                totalAmount = appt.Doctor?.ConsultationFee ?? 200000m;
            }

            var vm = new PaymentViewModel
            {
                AppointmentId = appt.Id,
                AppointmentCode = appt.AppointmentCode,
                InvoiceCode = appt.InvoiceCode ?? $"HD-{DateTime.Now:yyyyMMdd}-{appt.Id:D4}",
                PatientName = appt.Patient?.FullName ?? "Chưa rõ",
                PatientPhone = appt.Patient?.PhoneNumber ?? "—",
                DoctorName = $"{appt.Doctor?.Title} {appt.Doctor?.User?.FullName}".Trim(),
                SpecialtyName = appt.Specialty?.Name ?? "—",
                ConsultationFee = cost?.ConsultationFee ?? (appt.Doctor?.ConsultationFee ?? 200000m),
                TotalAmount = totalAmount,
                CashierName = User.Identity?.Name ?? "Kế toán viện phí",
                PaymentMethod = cost?.PaymentMethod ?? (appt.PaymentMethod ?? "Tiền mặt")
            };

            ViewBag.Cost = cost;
            return View(vm);
        }

        // ==========================================
        // US32: Xác nhận thu tiền & hoàn tất thanh toán
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProcessPayment(PaymentViewModel model)
        {
            var appt = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .FirstOrDefault(a => a.Id == model.AppointmentId);

            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ ca khám.";
                return RedirectToAction(nameof(Invoices));
            }

            var cost = _context.PatientCosts.FirstOrDefault(c => c.AppointmentId == model.AppointmentId);

            decimal finalPaid = model.TotalAmount > 0m ? model.TotalAmount : (cost?.TotalAmount ?? appt.Doctor?.ConsultationFee ?? 200000m);

            appt.PaymentStatus = "Paid";
            appt.PaymentMethod = model.PaymentMethod ?? "Tiền mặt";
            appt.PaidAmount = finalPaid;
            appt.PaidAt = DateTime.UtcNow;
            appt.InvoiceCode = string.IsNullOrWhiteSpace(model.InvoiceCode) ? $"HD-{DateTime.Now:yyyyMMdd}-{appt.Id:D4}" : model.InvoiceCode.Trim();
            appt.CashierName = User.Identity?.Name ?? "Kế toán thu ngân";

            if (cost != null)
            {
                cost.PaymentStatus = "Paid";
                cost.PaymentMethod = appt.PaymentMethod;
                cost.PaidAt = DateTime.UtcNow;
                cost.AccountantId ??= GetCurrentUserId();
            }

            // Gửi thông báo đến bệnh nhân
            if (appt.PatientId > 0)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = appt.PatientId,
                    Title = "Thanh toán viện phí thành công",
                    Message = $"Hóa đơn {appt.InvoiceCode} cho ca khám {appt.AppointmentCode} đã được thanh toán thành công ({finalPaid:N0} VNĐ). Cảm ơn quý khách!",
                    Type = "PaymentSuccess",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã thu tiền và xuất phiếu thu cho bệnh nhân {appt.Patient?.FullName} thành công!";
            return RedirectToAction(nameof(InvoiceReceipt), new { id = appt.Id });
        }

        // ==========================================
        // US32: Xác nhận thu tiền nhanh từ CostSheet
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAsPaid(int id, string? paymentMethod)
        {
            var cost = _context.PatientCosts
                .Include(c => c.Appointment)
                .ThenInclude(a => a!.Patient)
                .FirstOrDefault(c => c.Id == id);

            if (cost == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bảng tính chi phí.";
                return RedirectToAction(nameof(PatientCosts));
            }

            cost.PaymentStatus = "Paid";
            cost.PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Tiền mặt" : paymentMethod;
            cost.PaidAt = DateTime.UtcNow;
            cost.AccountantId ??= GetCurrentUserId();

            var appt = cost.Appointment;
            if (appt != null)
            {
                appt.PaymentStatus = "Paid";
                appt.PaymentMethod = cost.PaymentMethod;
                appt.PaidAmount = cost.TotalAmount;
                appt.PaidAt = DateTime.UtcNow;
                appt.CashierName = User.Identity?.Name ?? "Kế toán thu ngân";
                if (string.IsNullOrWhiteSpace(appt.InvoiceCode))
                {
                    appt.InvoiceCode = $"HD-{DateTime.Now:yyyyMMdd}-{appt.Id:D4}";
                }

                if (appt.PatientId > 0)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = appt.PatientId,
                        Title = "Thanh toán viện phí thành công",
                        Message = $"Hóa đơn {appt.InvoiceCode} cho ca khám {appt.AppointmentCode} đã được thanh toán thành công ({cost.TotalAmount:N0} VNĐ). Cảm ơn quý khách!",
                        Type = "PaymentSuccess",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã xác nhận thu tiền thành công cho ca khám {appt?.AppointmentCode}!";
            return RedirectToAction(nameof(InvoiceReceipt), new { id = appt?.Id ?? cost.AppointmentId });
        }

        [HttpGet]
        public IActionResult MarkAsPaid(int? id)
        {
            if (id.HasValue && id.Value > 0)
            {
                var cost = _context.PatientCosts.FirstOrDefault(c => c.Id == id.Value || c.AppointmentId == id.Value);
                if (cost != null)
                {
                    return RedirectToAction(nameof(CostSheet), new { id = cost.Id });
                }
            }
            return RedirectToAction(nameof(Invoices));
        }

        // ==========================================
        // US32: Hóa đơn / Phiếu thu viện phí (In & PDF)
        // ==========================================
        [HttpGet]
        [Authorize]
        public IActionResult InvoiceReceipt(int id)
        {
            var appt = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)!.ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == id);

            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin hóa đơn.";
                return RedirectToAction(nameof(Invoices));
            }

            var cost = _context.PatientCosts.FirstOrDefault(c => c.AppointmentId == id);
            ViewBag.Cost = cost;

            return View(appt);
        }

        // ==========================================
        // US34: Báo cáo doanh thu theo ngày / tháng
        // ==========================================
        public IActionResult RevenueReport(string? period, int? year, int? month, DateTime? date)
        {
            period ??= "month";
            int selectedYear = year ?? DateTime.Now.Year;
            int selectedMonth = month ?? DateTime.Now.Month;
            DateTime selectedDate = date ?? DateTime.Today;

            var paidAppts = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)!.ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .Where(a => a.PaymentStatus == "Paid")
                .ToList();

            var apptIds = paidAppts.Select(a => a.Id).ToList();
            var costs = _context.PatientCosts
                .Where(c => apptIds.Contains(c.AppointmentId))
                .ToDictionary(c => c.AppointmentId);

            List<Appointment> filteredAppts;
            var labels = new List<string>();
            var revenueData = new List<decimal>();
            var countData = new List<int>();

            if (period == "day")
            {
                filteredAppts = paidAppts.Where(a => (a.PaidAt?.ToLocalTime().Date ?? a.AppointmentDate.Date) == selectedDate.Date).ToList();

                // Biểu đồ theo các khung giờ trong ngày
                var hours = new[] { "07:00", "09:00", "11:00", "13:00", "15:00", "17:00" };
                foreach (var h in hours)
                {
                    labels.Add(h);
                    revenueData.Add(filteredAppts.Where(a => a.TimeSlot.StartsWith(h.Substring(0, 2))).Sum(a => a.PaidAmount));
                    countData.Add(filteredAppts.Count(a => a.TimeSlot.StartsWith(h.Substring(0, 2))));
                }
            }
            else if (period == "year")
            {
                filteredAppts = paidAppts.Where(a => (a.PaidAt?.Year ?? a.AppointmentDate.Year) == selectedYear).ToList();

                for (int m = 1; m <= 12; m++)
                {
                    labels.Add($"Tháng {m}");
                    var inMonth = filteredAppts.Where(a => (a.PaidAt?.Month ?? a.AppointmentDate.Month) == m).ToList();
                    revenueData.Add(inMonth.Sum(a => a.PaidAmount));
                    countData.Add(inMonth.Count);
                }
            }
            else // "month" default
            {
                filteredAppts = paidAppts.Where(a => (a.PaidAt?.Year ?? a.AppointmentDate.Year) == selectedYear &&
                                                     (a.PaidAt?.Month ?? a.AppointmentDate.Month) == selectedMonth).ToList();

                int daysInMonth = DateTime.DaysInMonth(selectedYear, selectedMonth);
                for (int d = 1; d <= daysInMonth; d++)
                {
                    labels.Add($"{d:D2}/{selectedMonth:D2}");
                    var inDay = filteredAppts.Where(a => (a.PaidAt?.Day ?? a.AppointmentDate.Day) == d).ToList();
                    revenueData.Add(inDay.Sum(a => a.PaidAmount));
                    countData.Add(inDay.Count);
                }
            }

            // KPIs
            decimal totalRevenue = filteredAppts.Sum(a => a.PaidAmount);
            int totalTransactions = filteredAppts.Count;

            // Bóc tách theo nhóm chi phí
            decimal consultationRev = 0m;
            decimal serviceRev = 0m;
            decimal labRev = 0m;
            decimal prescriptionRev = 0m;

            foreach (var a in filteredAppts)
            {
                if (costs.ContainsKey(a.Id))
                {
                    var c = costs[a.Id];
                    consultationRev += c.ConsultationFee;
                    serviceRev += c.ServiceFee;
                    labRev += c.LabFee;
                    prescriptionRev += c.PrescriptionFee;
                }
                else
                {
                    consultationRev += a.Doctor?.ConsultationFee ?? 200000m;
                }
            }

            // Phương thức thanh toán
            var cashRevenue = filteredAppts.Where(a => a.PaymentMethod == "Tiền mặt" || a.PaymentMethod == "Cash").Sum(a => a.PaidAmount);
            var bankRevenue = filteredAppts.Where(a => a.PaymentMethod == "Chuyển khoản" || a.PaymentMethod == "Bank Transfer").Sum(a => a.PaidAmount);
            var cardRevenue = filteredAppts.Where(a => a.PaymentMethod == "Thẻ" || a.PaymentMethod == "Card").Sum(a => a.PaidAmount);

            // Doanh thu theo chuyên khoa
            var specialtyRevenue = filteredAppts
                .GroupBy(a => a.Specialty?.Name ?? "Chung")
                .ToDictionary(g => g.Key, g => g.Sum(a => a.PaidAmount));

            // Doanh thu theo bác sĩ
            var doctorRevenue = filteredAppts
                .GroupBy(a => $"{a.Doctor?.Title} {a.Doctor?.User?.FullName}".Trim())
                .ToDictionary(g => string.IsNullOrWhiteSpace(g.Key) ? "Bác sĩ khác" : g.Key, g => g.Sum(a => a.PaidAmount));

            ViewBag.Period = period;
            ViewBag.SelectedYear = selectedYear;
            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalTransactions = totalTransactions;
            ViewBag.ConsultationRev = consultationRev;
            ViewBag.ServiceRev = serviceRev;
            ViewBag.LabRev = labRev;
            ViewBag.PrescriptionRev = prescriptionRev;
            ViewBag.CashRevenue = cashRevenue;
            ViewBag.BankRevenue = bankRevenue;
            ViewBag.CardRevenue = cardRevenue;
            ViewBag.SpecialtyRevenue = specialtyRevenue;
            ViewBag.DoctorRevenue = doctorRevenue;
            ViewBag.Costs = costs;

            ViewBag.LabelsJson = System.Text.Json.JsonSerializer.Serialize(labels);
            ViewBag.RevenueJson = System.Text.Json.JsonSerializer.Serialize(revenueData);
            ViewBag.CountJson = System.Text.Json.JsonSerializer.Serialize(countData);

            return View(filteredAppts);
        }
    }
}
