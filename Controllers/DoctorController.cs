using System;
using System.Linq;
using System.Security.Claims;
using Healthy_System.Data;
using Healthy_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Healthy_System.Controllers
{
    [Authorize(Roles = "Doctor")]
    public class DoctorController : Controller
    {
        private readonly AppDbContext _context;

        public DoctorController(AppDbContext context)
        {
            _context = context;
        }

        private Doctor? GetCurrentDoctor()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return null;

            return _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .FirstOrDefault(d => d.UserId == userId);
        }

        // US-30: View Doctor Dashboard & Queue
        public IActionResult Dashboard()
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null)
            {
                // Fallback for Admin testing
                doctor = _context.Doctors.Include(d => d.User).Include(d => d.Specialty).FirstOrDefault();
            }

            if (doctor == null)
            {
                return Content("Không tìm thấy thông tin bác sĩ liên kết với tài khoản này.");
            }

            var today = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);

            // Appointments today
            var todayAppointments = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Specialty)
                .Where(a => a.DoctorId == doctor.Id && a.AppointmentDate.Date == today)
                .OrderBy(a => a.TimeSlot)
                .ToList();

            ViewBag.Doctor = doctor;
            ViewBag.TodayAppointments = todayAppointments;
            ViewBag.TotalToday = todayAppointments.Count;
            ViewBag.WaitingCount = todayAppointments.Count(a => a.Status == "Pending" || a.Status == "Confirmed");
            ViewBag.CompletedCount = todayAppointments.Count(a => a.Status == "Completed");

            // Upcoming appointments
            ViewBag.UpcomingAppointments = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Specialty)
                .Where(a => a.DoctorId == doctor.Id && a.AppointmentDate.Date > today && a.Status != "Cancelled")
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.TimeSlot)
                .Take(10)
                .ToList();

            return View();
        }

        // US-30: View Patient Queue for today
        public IActionResult Queue()
        {
            var doctor = GetCurrentDoctor() ?? _context.Doctors.Include(d => d.User).FirstOrDefault();
            if (doctor == null) return NotFound();

            var today = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);
            var queue = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Specialty)
                .Where(a => a.DoctorId == doctor.Id && a.AppointmentDate.Date == today)
                .OrderBy(a => a.TimeSlot)
                .ToList();

            ViewBag.Doctor = doctor;
            return View(queue);
        }

        // US-31 & US-34: Clinical Examination & e-Prescription
        public IActionResult Examination(int id)
        {
            var doctor = GetCurrentDoctor() ?? _context.Doctors.FirstOrDefault();
            var appointment = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == id);

            if (appointment == null) return NotFound();

            // Medical history of this patient
            ViewBag.PatientHistory = _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .Where(a => a.PatientId == appointment.PatientId && a.Id != appointment.Id)
                .OrderByDescending(a => a.AppointmentDate)
                .ToList();

            ViewBag.Doctor = doctor;
            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CompleteExamination(int appointmentId, string diagnosis, string prescription, string? labRequest, string? doctorNotes)
        {
            var appt = _context.Appointments.Include(a => a.Patient).FirstOrDefault(a => a.Id == appointmentId);
            if (appt == null) return NotFound();

            appt.Status = "Completed";
            appt.Diagnosis = diagnosis?.Trim();
            appt.Prescription = prescription?.Trim();
            appt.LabRequest = labRequest?.Trim();
            appt.DoctorNotes = doctorNotes?.Trim();
            appt.UpdatedAt = DateTime.UtcNow;

            // Log notification to patient
            _context.Notifications.Add(new Notification
            {
                UserId = appt.PatientId,
                Title = "Hoàn thành ca khám bệnh",
                Message = $"Bác sĩ đã hoàn tất kết luận khám cho lịch hẹn {appt.AppointmentCode}. Chẩn đoán: {diagnosis}. Bạn có thể vào mục Hồ sơ khám bệnh & Đơn thuốc để xem chi tiết.",
                Type = "StatusUpdate",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã hoàn thành ca khám bệnh cho bệnh nhân {appt.Patient?.FullName}!";
            return RedirectToAction(nameof(MedicalRecord), new { id = appt.Id });
        }

        // US27: Bác sĩ thay đổi hoặc hủy ca khám khẩn cấp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelEmergency(int appointmentId, string emergencyReason)
        {
            var doctor = GetCurrentDoctor() ?? _context.Doctors.FirstOrDefault();
            var appt = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .FirstOrDefault(a => a.Id == appointmentId);

            if (appt == null) return NotFound();

            if (string.IsNullOrWhiteSpace(emergencyReason))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập lý do hủy ca khám khẩn cấp.";
                return RedirectToAction(nameof(Queue));
            }

            appt.Status = "Cancelled";
            appt.CancellationReason = $"[Bác sĩ hủy khẩn cấp] {emergencyReason.Trim()}";
            appt.CancelledAt = DateTime.UtcNow;
            appt.UpdatedAt = DateTime.UtcNow;

            // Gửi thông báo khẩn cấp cho bệnh nhân
            _context.Notifications.Add(new Notification
            {
                UserId = appt.PatientId,
                Title = "⚠️ [KHẨN CẤP] Bác sĩ thông báo hủy ca khám",
                Message = $"Ca khám mã {appt.AppointmentCode} vào ngày {appt.AppointmentDate:dd/MM/yyyy} ({appt.TimeSlot}) đã bị Bác sĩ hủy khẩn cấp. Lý do: {emergencyReason}. Phòng khám chân thành cáo lỗi và sẽ ưu tiên xếp lại lịch mới cho quý khách.",
                Type = "EmergencyAlert",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã hủy khẩn cấp ca khám {appt.AppointmentCode} của bệnh nhân {appt.Patient?.FullName}. Hệ thống đã gửi thông báo đến bệnh nhân.";
            return RedirectToAction(nameof(Queue));
        }

        // US27: Bác sĩ dời ca khám khẩn cấp sang ngày/giờ khác
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RescheduleEmergency(int appointmentId, DateTime newDate, string newTimeSlot, string emergencyReason)
        {
            var doctor = GetCurrentDoctor() ?? _context.Doctors.FirstOrDefault();
            var appt = _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefault(a => a.Id == appointmentId);

            if (appt == null) return NotFound();

            if (newDate.Date < DateTime.Today)
            {
                TempData["ErrorMessage"] = "Ngày dời lịch mới không thể ở trong quá khứ.";
                return RedirectToAction(nameof(Queue));
            }

            var oldDateStr = appt.AppointmentDate.ToString("dd/MM/yyyy");
            var oldTimeSlot = appt.TimeSlot;

            appt.AppointmentDate = DateTime.SpecifyKind(newDate.Date, DateTimeKind.Utc);
            appt.TimeSlot = newTimeSlot;
            appt.UpdatedAt = DateTime.UtcNow;

            // Thông báo khẩn cấp cho bệnh nhân
            _context.Notifications.Add(new Notification
            {
                UserId = appt.PatientId,
                Title = "⚠️ [KHẨN CẤP] Bác sĩ điều chỉnh lịch hẹn",
                Message = $"Lịch khám mã {appt.AppointmentCode} đã được Bác sĩ dời từ {oldDateStr} ({oldTimeSlot}) sang ngày {newDate:dd/MM/yyyy} ({newTimeSlot}). Lý do khẩn cấp: {emergencyReason}.",
                Type = "EmergencyAlert",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã cập nhật dời ca khám {appt.AppointmentCode} sang {newDate:dd/MM/yyyy} ({newTimeSlot}). Hệ thống đã gửi thông báo khẩn đến bệnh nhân.";
            return RedirectToAction(nameof(Queue));
        }

        // US06: Xem danh sách hồ sơ khám bệnh, đơn thuốc đã khám
        public IActionResult MedicalRecords(string? searchKeyword)
        {
            var doctor = GetCurrentDoctor() ?? _context.Doctors.FirstOrDefault();
            if (doctor == null) return NotFound();

            var query = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Specialty)
                .Where(a => a.DoctorId == doctor.Id && a.Status == "Completed");

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                searchKeyword = searchKeyword.Trim().ToLower();
                query = query.Where(a => 
                    (a.Patient != null && a.Patient.FullName.ToLower().Contains(searchKeyword)) ||
                    a.AppointmentCode.ToLower().Contains(searchKeyword) ||
                    (a.Diagnosis != null && a.Diagnosis.ToLower().Contains(searchKeyword)));
            }

            var list = query
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.UpdatedAt)
                .ToList();

            ViewBag.Doctor = doctor;
            ViewBag.SearchKeyword = searchKeyword;
            return View(list);
        }

        // US06: Xem chi tiết hồ sơ khám bệnh & đơn thuốc
        public IActionResult MedicalRecord(int id)
        {
            var doctor = GetCurrentDoctor() ?? _context.Doctors.FirstOrDefault();
            var appt = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Doctor).ThenInclude(d => d!.Specialty)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == id);

            if (appt == null) return NotFound();

            ViewBag.Doctor = doctor;
            return View(appt);
        }

        // US-35: Doctor Work Schedule
        public IActionResult Schedule()
        {
            var doctor = GetCurrentDoctor() ?? _context.Doctors.FirstOrDefault();
            if (doctor == null) return NotFound();

            var today = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);
            var schedules = _context.DoctorSchedules
                .Where(s => s.DoctorId == doctor.Id && s.WorkDate >= today)
                .OrderBy(s => s.WorkDate)
                .ThenBy(s => s.TimeSlot)
                .ToList();

            ViewBag.Doctor = doctor;
            return View(schedules);
        }

        // US26: Doctor Profile — đầy đủ fields
        public IActionResult Profile()
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null) return NotFound();

            ViewBag.Specialties = _context.Specialties.OrderBy(s => s.Name).ToList();
            return View(doctor);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Profile(int id, string fullName, string phone, string title,
            int specialtyId, int experienceYears, string roomNumber, decimal consultationFee, string bio)
        {
            var doctor = _context.Doctors.Include(d => d.User).FirstOrDefault(d => d.Id == id);
            if (doctor == null) return NotFound();

            // Cập nhật User
            if (doctor.User != null)
            {
                doctor.User.FullName = fullName?.Trim() ?? doctor.User.FullName;
                doctor.User.PhoneNumber = phone?.Trim() ?? doctor.User.PhoneNumber;
            }

            // Cập nhật Doctor
            doctor.Title = title?.Trim() ?? doctor.Title;
            doctor.SpecialtyId = specialtyId;
            doctor.ExperienceYears = experienceYears;
            doctor.RoomNumber = roomNumber?.Trim() ?? doctor.RoomNumber;
            doctor.ConsultationFee = consultationFee;
            doctor.Bio = bio?.Trim() ?? string.Empty;

            _context.SaveChanges();
            TempData["SuccessMessage"] = "Cập nhật hồ sơ chuyên môn thành công!";
            return RedirectToAction(nameof(Profile));
        }
    }
}
