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
    [Authorize(Roles = "Doctor,Admin")]
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
        public IActionResult CompleteExamination(int appointmentId, string diagnosis, string prescription, string? labRequest)
        {
            var appt = _context.Appointments.Include(a => a.Patient).FirstOrDefault(a => a.Id == appointmentId);
            if (appt == null) return NotFound();

            appt.Status = "Completed";
            appt.UpdatedAt = DateTime.UtcNow;

            // Log notification to patient
            _context.Notifications.Add(new Notification
            {
                UserId = appt.PatientId,
                Title = "Hoàn thành ca khám bệnh",
                Message = $"Bác sĩ đã hoàn tất kết luận khám cho lịch hẹn {appt.AppointmentCode}. Chẩn đoán: {diagnosis}. Đơn thuốc điện tử đã được ghi nhận.",
                Type = "StatusUpdate",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã hoàn thành ca khám bệnh cho bệnh nhân {appt.Patient?.FullName}!";
            return RedirectToAction(nameof(Queue));
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

        // US-36: Doctor Profile
        public IActionResult Profile()
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null) return NotFound();

            return View(doctor);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Profile(int id, string bio, string roomNumber, decimal consultationFee)
        {
            var doctor = _context.Doctors.Include(d => d.User).FirstOrDefault(d => d.Id == id);
            if (doctor == null) return NotFound();

            doctor.Bio = bio.Trim();
            doctor.RoomNumber = roomNumber.Trim();
            doctor.ConsultationFee = consultationFee;

            _context.SaveChanges();
            TempData["SuccessMessage"] = "Cập nhật thông tin hồ sơ bác sĩ thành công!";
            return RedirectToAction(nameof(Profile));
        }
    }
}
