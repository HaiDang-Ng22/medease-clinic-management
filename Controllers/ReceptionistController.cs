using System;
using System.Collections.Generic;
using System.Linq;
using Healthy_System.Data;
using Healthy_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Healthy_System.Controllers
{
    [Authorize(Roles = "Receptionist")]
    public class ReceptionistController : Controller
    {
        private readonly AppDbContext _context;

        public ReceptionistController(AppDbContext context)
        {
            _context = context;
        }

        // US-22: Reception Dashboard
        public IActionResult Dashboard()
        {
            var today = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);

            var todayAppointments = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .Where(a => a.AppointmentDate.Date == today)
                .OrderBy(a => a.TimeSlot)
                .ToList();

            ViewBag.TodayAppointments = todayAppointments;
            ViewBag.TotalToday = todayAppointments.Count;
            ViewBag.PendingConfirmation = _context.Appointments.Count(a => a.Status == "Pending");
            ViewBag.ConfirmedToday = todayAppointments.Count(a => a.Status == "Confirmed");
            ViewBag.CompletedToday = todayAppointments.Count(a => a.Status == "Completed");

            return View();
        }

        // US-22: Full Appointments List
        public IActionResult Appointments(string? status, string? search)
        {
            var query = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(a => a.AppointmentCode.ToLower().Contains(term)
                                      || (a.Patient != null && a.Patient.FullName.ToLower().Contains(term))
                                      || (a.Patient != null && a.Patient.PhoneNumber != null && a.Patient.PhoneNumber.Contains(term)));
            }

            ViewBag.SelectedStatus = status;
            ViewBag.SearchTerm = search;

            var list = query
                .OrderByDescending(a => a.AppointmentDate)
                .ThenBy(a => a.TimeSlot)
                .Take(50)
                .ToList();

            return View(list);
        }

        // US-27: Confirm Online Appointment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Confirm(int id)
        {
            var appt = _context.Appointments.Include(a => a.Patient).FirstOrDefault(a => a.Id == id);
            if (appt == null) return NotFound();

            appt.Status = "Confirmed";
            appt.UpdatedAt = DateTime.UtcNow;

            _context.Notifications.Add(new Notification
            {
                UserId = appt.PatientId,
                Title = "Lịch khám đã được tiếp nhận xác nhận",
                Message = $"Lễ tân đã xác nhận lịch hẹn {appt.AppointmentCode} của bạn ({appt.TimeSlot} ngày {appt.AppointmentDate:dd/MM/yyyy}). Vui lòng đến đúng giờ.",
                Type = "StatusUpdate",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            _context.SaveChanges();
            TempData["SuccessMessage"] = $"Đã xác nhận lịch hẹn {appt.AppointmentCode} thành công!";
            return RedirectToAction(nameof(Dashboard));
        }

        // US-15: Create Walk-in OR Phone Appointment at Reception
        [HttpGet]
        public IActionResult CreateAppointment()
        {
            ViewBag.Specialties = _context.Specialties.Where(s => s.IsActive).ToList();
            ViewBag.Doctors = _context.Doctors.Include(d => d.User).ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAppointment(string patientName, string phone, int specialtyId, int doctorId,
            DateTime appointmentDate, string timeSlot, string reason, string bookingType)
        {
            // Find patient by phone, or create new guest patient account
            var patient = _context.Users.FirstOrDefault(u => u.PhoneNumber == phone.Trim());
            if (patient == null)
            {
                patient = new User
                {
                    FullName = patientName.Trim(),
                    Email = $"khach.{phone.Trim()}@healthysystem.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                    PhoneNumber = phone.Trim(),
                    Role = "Patient",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Users.Add(patient);
                _context.SaveChanges();
            }
            else if (!string.IsNullOrWhiteSpace(patientName))
            {
                // Update display name in case receptionist corrected it
                patient.FullName = patientName.Trim();
                _context.SaveChanges();
            }

            // BUG04 Fix: Kiểm tra trùng khung giờ khám trước khi tạo lịch hẹn
            var utcDate = DateTime.SpecifyKind(appointmentDate.Date, DateTimeKind.Utc);
            bool slotConflict = _context.Appointments.Any(a =>
                a.DoctorId == doctorId &&
                a.AppointmentDate.Date == utcDate.Date &&
                a.TimeSlot == timeSlot &&
                a.Status != "Cancelled");

            if (slotConflict)
            {
                TempData["ErrorMessage"] = $"Khung giờ {timeSlot} ngày {appointmentDate:dd/MM/yyyy} của bác sĩ này đã có bệnh nhân đặt trước. Vui lòng chọn khung giờ khác.";
                ViewBag.Specialties = _context.Specialties.Where(s => s.IsActive).ToList();
                ViewBag.Doctors = _context.Doctors.Include(d => d.User).ToList();
                return View();
            }

            // US-15: use different code prefix to distinguish walk-in vs phone booking
            var prefix = bookingType == "Phone" ? "HS-TEL" : "HS-REC";
            var apptCode = $"{prefix}-{new Random().Next(1000, 9999)}";

            var defaultReason = bookingType == "Phone"
                ? "Đặt lịch qua điện thoại tại Lễ tân"
                : "Tiếp nhận trực tiếp tại quầy Lễ tân";

            var appt = new Appointment
            {
                AppointmentCode = apptCode,
                PatientId = patient.Id,
                DoctorId = doctorId,
                SpecialtyId = specialtyId,
                AppointmentDate = utcDate,
                TimeSlot = timeSlot,
                ReasonForVisit = string.IsNullOrWhiteSpace(reason) ? defaultReason : reason.Trim(),
                Status = "Confirmed",
                CreatedAt = DateTime.UtcNow
            };

            _context.Appointments.Add(appt);
            _context.SaveChanges();

            var typeLabel = bookingType == "Phone" ? "qua điện thoại" : "tại quầy";
            TempData["SuccessMessage"] = $"Tiếp nhận bệnh nhân thành công ({typeLabel})! Mã cuộc hẹn: {apptCode}";
            return RedirectToAction(nameof(Dashboard));
        }

        // US-16: Search & Look Up Patient Information
        [HttpGet]
        public IActionResult SearchPatient(string? query)
        {
            ViewBag.Query = query;

            if (string.IsNullOrWhiteSpace(query))
            {
                ViewBag.Patients = null;
                ViewBag.AppointmentsByPatient = new Dictionary<int, List<Appointment>>();
                return View();
            }

            var term = query.Trim().ToLower();
            var normalizedTerm = RemoveDiacritics(term);

            // BUG05 Fix: Tìm kiếm bệnh nhân hỗ trợ cả có dấu và không dấu
            var allPatients = _context.Users.Where(u => u.Role == "Patient").ToList();
            var patients = allPatients
                .Where(u =>
                    (!string.IsNullOrWhiteSpace(u.FullName) && (u.FullName.ToLower().Contains(term) || RemoveDiacritics(u.FullName).Contains(normalizedTerm))) ||
                    (!string.IsNullOrWhiteSpace(u.PhoneNumber) && u.PhoneNumber.Contains(term)) ||
                    (!string.IsNullOrWhiteSpace(u.Email) && u.Email.ToLower().Contains(term)))
                .OrderBy(u => u.FullName)
                .ToList();

            var patientIds = patients.Select(p => p.Id).ToList();

            var appointments = _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .Where(a => patientIds.Contains(a.PatientId))
                .OrderByDescending(a => a.AppointmentDate)
                .ToList();

            ViewBag.Patients = patients;
            ViewBag.AppointmentsByPatient = appointments
                .GroupBy(a => a.PatientId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return View();
        }

        // US-17: Cancel Appointment at Reception Counter (GET - show confirmation form)
        [HttpGet]
        public IActionResult CancelAppointment(int id)
        {
            var appt = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == id);

            if (appt == null) return NotFound();

            if (appt.Status == "Completed" || appt.Status == "Cancelled")
            {
                TempData["ErrorMessage"] = "Lịch hẹn này không thể hủy (đã hoàn thành hoặc đã hủy trước đó).";
                return RedirectToAction(nameof(Appointments));
            }

            return View(appt);
        }

        // US-17: Cancel Appointment at Reception Counter (POST - perform cancellation)
        [HttpPost, ActionName("CancelAppointment")]
        [ValidateAntiForgeryToken]
        public IActionResult CancelAppointmentConfirmed(int id, string cancellationReason)
        {
            var appt = _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefault(a => a.Id == id);

            if (appt == null) return NotFound();

            if (appt.Status == "Completed" || appt.Status == "Cancelled")
            {
                TempData["ErrorMessage"] = "Lịch hẹn này không thể hủy.";
                return RedirectToAction(nameof(Appointments));
            }

            appt.Status = "Cancelled";
            appt.CancellationReason = string.IsNullOrWhiteSpace(cancellationReason)
                ? "Hủy theo yêu cầu tại quầy lễ tân"
                : cancellationReason.Trim();
            appt.CancelledAt = DateTime.UtcNow;
            appt.UpdatedAt = DateTime.UtcNow;

            // Notify patient
            _context.Notifications.Add(new Notification
            {
                UserId = appt.PatientId,
                Title = "Lịch hẹn đã bị hủy",
                Message = $"Lịch hẹn {appt.AppointmentCode} ngày {appt.AppointmentDate:dd/MM/yyyy} đã được hủy tại quầy lễ tân. Lý do: {appt.CancellationReason}. Liên hệ phòng khám nếu cần hỗ trợ.",
                Type = "StatusUpdate",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            _context.SaveChanges();
            TempData["SuccessMessage"] = $"Đã hủy lịch hẹn {appt.AppointmentCode} thành công!";
            return RedirectToAction(nameof(Appointments));
        }

        // US-29: View All Doctors Schedule
        public IActionResult DoctorsSchedule()
        {
            var doctors = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .Include(d => d.Schedules.Where(s => s.WorkDate >= DateTime.Today))
                .ToList();

            return View(doctors);
        }

        // Helper method: Loại bỏ dấu tiếng Việt để phục vụ tìm kiếm không phân biệt dấu
        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLower();
        }
    }
}
