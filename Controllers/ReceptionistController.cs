using System;
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

        // US-23: Create Walk-in Appointment at Reception
        public IActionResult CreateAppointment()
        {
            ViewBag.Specialties = _context.Specialties.Where(s => s.IsActive).ToList();
            ViewBag.Doctors = _context.Doctors.Include(d => d.User).ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAppointment(string patientName, string phone, int specialtyId, int doctorId, DateTime appointmentDate, string timeSlot, string reason)
        {
            // Find or create patient
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

            var randomCode = new Random().Next(1000, 9999);
            var apptCode = $"HS-REC-{randomCode}";

            var appt = new Appointment
            {
                AppointmentCode = apptCode,
                PatientId = patient.Id,
                DoctorId = doctorId,
                SpecialtyId = specialtyId,
                AppointmentDate = DateTime.SpecifyKind(appointmentDate.Date, DateTimeKind.Utc),
                TimeSlot = timeSlot,
                ReasonForVisit = string.IsNullOrWhiteSpace(reason) ? "Tiếp nhận trực tiếp tại quầy Lễ tân" : reason.Trim(),
                Status = "Confirmed",
                CreatedAt = DateTime.UtcNow
            };

            _context.Appointments.Add(appt);
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Tiếp nhận bệnh nhân thành công! Mã cuộc hẹn: {apptCode}";
            return RedirectToAction(nameof(Dashboard));
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
    }
}
