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
    [Authorize]
    public class AppointmentController : Controller
    {
        private readonly AppDbContext _context;

        public AppointmentController(AppDbContext context)
        {
            _context = context;
        }

        public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Admin"))
                {
                    filterContext.Result = RedirectToAction("Dashboard", "Admin");
                }
                else if (User.IsInRole("Doctor"))
                {
                    filterContext.Result = RedirectToAction("Dashboard", "Doctor");
                }
                else if (User.IsInRole("Receptionist"))
                {
                    filterContext.Result = RedirectToAction("Dashboard", "Receptionist");
                }
            }
        }

        // US-10: Book Appointment Online (GET)
        [HttpGet]
        public IActionResult Book(int? doctorId, int? specialtyId)
        {

            var model = new BookAppointmentViewModel
            {
                Specialties = _context.Specialties.Where(s => s.IsActive).ToList(),
                AppointmentDate = DateTime.Today.AddDays(1)
            };

            if (specialtyId.HasValue && specialtyId.Value > 0)
            {
                model.SpecialtyId = specialtyId.Value;
                model.Doctors = _context.Doctors
                    .Include(d => d.User)
                    .Where(d => d.SpecialtyId == specialtyId.Value)
                    .ToList();
            }
            else
            {
                model.Doctors = _context.Doctors
                    .Include(d => d.User)
                    .ToList();
            }

            if (doctorId.HasValue && doctorId.Value > 0)
            {
                var doc = _context.Doctors.FirstOrDefault(d => d.Id == doctorId.Value);
                if (doc != null)
                {
                    model.DoctorId = doc.Id;
                    model.SpecialtyId = doc.SpecialtyId;
                }
            }

            return View(model);
        }

        // US-10: Book Appointment Online (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Book(BookAppointmentViewModel model)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int patientId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (model.AppointmentDate.Date < DateTime.Today)
            {
                ModelState.AddModelError("AppointmentDate", "Ngày khám không được chọn trong quá khứ.");
            }

            if (!ModelState.IsValid)
            {
                model.Specialties = _context.Specialties.Where(s => s.IsActive).ToList();
                model.Doctors = _context.Doctors.Include(d => d.User).Where(d => d.SpecialtyId == model.SpecialtyId).ToList();
                return View(model);
            }

            // Check doctor & schedule slot
            var doctor = _context.Doctors.Include(d => d.User).Include(d => d.Specialty).FirstOrDefault(d => d.Id == model.DoctorId);
            if (doctor == null)
            {
                ModelState.AddModelError("DoctorId", "Bác sĩ được chọn không hợp lệ.");
                model.Specialties = _context.Specialties.Where(s => s.IsActive).ToList();
                return View(model);
            }

            // Find or create schedule slot
            var schedule = _context.DoctorSchedules.FirstOrDefault(s =>
                s.DoctorId == model.DoctorId &&
                s.WorkDate.Date == model.AppointmentDate.Date &&
                s.TimeSlot == model.TimeSlot);

            if (schedule != null && !schedule.IsAvailable)
            {
                ModelState.AddModelError("TimeSlot", "Khung giờ này vừa có người đặt. Vui lòng chọn khung giờ khác.");
                model.Specialties = _context.Specialties.Where(s => s.IsActive).ToList();
                model.Doctors = _context.Doctors.Include(d => d.User).Where(d => d.SpecialtyId == model.SpecialtyId).ToList();
                return View(model);
            }

            // Generate unique appointment code
            var randomCode = new Random().Next(1000, 9999);
            var apptCode = $"HS-{DateTime.Now:yyMM}-{randomCode}";

            var appointment = new Appointment
            {
                AppointmentCode = apptCode,
                PatientId = patientId,
                DoctorId = model.DoctorId,
                SpecialtyId = model.SpecialtyId,
                AppointmentDate = model.AppointmentDate.Date,
                TimeSlot = model.TimeSlot,
                ReasonForVisit = model.ReasonForVisit.Trim(),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Appointments.Add(appointment);

            // Update or create schedule
            if (schedule != null)
            {
                schedule.BookedPatients += 1;
                schedule.IsAvailable = false;
            }
            else
            {
                _context.DoctorSchedules.Add(new DoctorSchedule
                {
                    DoctorId = model.DoctorId,
                    WorkDate = model.AppointmentDate.Date,
                    TimeSlot = model.TimeSlot,
                    MaxPatients = 1,
                    BookedPatients = 1,
                    IsAvailable = false
                });
            }

            // US-18: Add Reminder/Notification for Patient
            _context.Notifications.Add(new Notification
            {
                UserId = patientId,
                Title = "Đặt lịch hẹn thành công!",
                Message = $"Bạn đã đặt lịch hẹn khám {apptCode} vào {model.TimeSlot} ngày {model.AppointmentDate:dd/MM/yyyy} với {doctor.Title} {doctor.User?.FullName}. Nhân viên phòng khám sẽ liên hệ xác nhận sớm nhất.",
                Type = "Reminder",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đặt lịch khám thành công! Mã cuộc hẹn của bạn là: {apptCode}";
            return RedirectToAction(nameof(Details), new { id = appointment.Id });
        }

        // US-11: View My Appointments
        [HttpGet]
        public IActionResult MyAppointments(string? status)
        {
            if (User.IsInRole("Doctor")) return RedirectToAction("Dashboard", "Doctor");
            if (User.IsInRole("Receptionist")) return RedirectToAction("Appointments", "Receptionist");
            if (User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Admin");

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int patientId))
            {
                return RedirectToAction("Login", "Account");
            }

            var query = _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .Where(a => a.PatientId == patientId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status == status);
            }

            ViewBag.SelectedStatus = status;

            var appointments = query
                .OrderByDescending(a => a.AppointmentDate)
                .ThenBy(a => a.TimeSlot)
                .ToList();

            return View(appointments);
        }

        // Appointment Details Voucher
        [HttpGet]
        public IActionResult Details(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int patientId))
            {
                return RedirectToAction("Login", "Account");
            }

            var appointment = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == id && a.PatientId == patientId);

            if (appointment == null)
            {
                return NotFound();
            }

            // Check if can reschedule or cancel (> 2 hours)
            ViewBag.CanModify = CanModifyAppointment(appointment.AppointmentDate, appointment.TimeSlot, appointment.Status);

            return View(appointment);
        }

        // US-12: Reschedule Appointment (GET)
        [HttpGet]
        public IActionResult Reschedule(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int patientId))
            {
                return RedirectToAction("Login", "Account");
            }

            var appt = _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == id && a.PatientId == patientId);

            if (appt == null)
            {
                return NotFound();
            }

            // Enforce business rule: only allow change >= 2 hours prior
            if (!CanModifyAppointment(appt.AppointmentDate, appt.TimeSlot, appt.Status))
            {
                TempData["ErrorMessage"] = "Theo quy định, lịch hẹn chỉ có thể thay đổi trước giờ khám ít nhất 2 tiếng!";
                return RedirectToAction(nameof(Details), new { id = appt.Id });
            }

            var model = new RescheduleViewModel
            {
                AppointmentId = appt.Id,
                AppointmentCode = appt.AppointmentCode,
                DoctorName = $"{appt.Doctor?.Title} {appt.Doctor?.User?.FullName}",
                SpecialtyName = appt.Specialty?.Name ?? string.Empty,
                CurrentDate = appt.AppointmentDate,
                CurrentTimeSlot = appt.TimeSlot,
                NewDate = DateTime.Today.AddDays(1)
            };

            return View(model);
        }

        // US-12: Reschedule Appointment (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reschedule(RescheduleViewModel model)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int patientId))
            {
                return RedirectToAction("Login", "Account");
            }

            var appt = _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefault(a => a.Id == model.AppointmentId && a.PatientId == patientId);

            if (appt == null)
            {
                return NotFound();
            }

            if (!CanModifyAppointment(appt.AppointmentDate, appt.TimeSlot, appt.Status))
            {
                TempData["ErrorMessage"] = "Không thể đổi lịch vì thời gian khám còn dưới 2 tiếng hoặc lịch hẹn không hợp lệ!";
                return RedirectToAction(nameof(Details), new { id = appt.Id });
            }

            if (model.NewDate.Date < DateTime.Today)
            {
                ModelState.AddModelError("NewDate", "Ngày khám mới không hợp lệ.");
                return View(model);
            }

            // Free previous schedule slot
            var oldSchedule = _context.DoctorSchedules.FirstOrDefault(s =>
                s.DoctorId == appt.DoctorId &&
                s.WorkDate.Date == appt.AppointmentDate.Date &&
                s.TimeSlot == appt.TimeSlot);

            if (oldSchedule != null)
            {
                oldSchedule.BookedPatients = Math.Max(0, oldSchedule.BookedPatients - 1);
                oldSchedule.IsAvailable = true;
            }

            // Reserve new slot
            var newSchedule = _context.DoctorSchedules.FirstOrDefault(s =>
                s.DoctorId == appt.DoctorId &&
                s.WorkDate.Date == model.NewDate.Date &&
                s.TimeSlot == model.NewTimeSlot);

            if (newSchedule != null && !newSchedule.IsAvailable)
            {
                ModelState.AddModelError("NewTimeSlot", "Khung giờ mới này đã có người đặt.");
                return View(model);
            }

            if (newSchedule != null)
            {
                newSchedule.BookedPatients += 1;
                newSchedule.IsAvailable = false;
            }

            // Update appointment
            appt.AppointmentDate = model.NewDate.Date;
            appt.TimeSlot = model.NewTimeSlot;
            appt.Status = "Pending";
            appt.UpdatedAt = DateTime.UtcNow;

            // Notification
            _context.Notifications.Add(new Notification
            {
                UserId = patientId,
                Title = "Thay đổi lịch hẹn thành công",
                Message = $"Lịch hẹn {appt.AppointmentCode} của bạn đã được chuyển sang {model.NewTimeSlot} ngày {model.NewDate:dd/MM/yyyy}.",
                Type = "StatusUpdate",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            _context.SaveChanges();

            TempData["SuccessMessage"] = "Thay đổi thời gian khám bệnh thành công!";
            return RedirectToAction(nameof(Details), new { id = appt.Id });
        }

        // US-13: Cancel Appointment (GET)
        [HttpGet]
        public IActionResult Cancel(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int patientId))
            {
                return RedirectToAction("Login", "Account");
            }

            var appt = _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefault(a => a.Id == id && a.PatientId == patientId);

            if (appt == null)
            {
                return NotFound();
            }

            if (!CanModifyAppointment(appt.AppointmentDate, appt.TimeSlot, appt.Status))
            {
                TempData["ErrorMessage"] = "Theo quy định của Healthy System, lịch hẹn chỉ có thể hủy trước giờ khám ít nhất 2 tiếng!";
                return RedirectToAction(nameof(Details), new { id = appt.Id });
            }

            var model = new CancelAppointmentViewModel
            {
                AppointmentId = appt.Id,
                AppointmentCode = appt.AppointmentCode,
                DoctorName = $"{appt.Doctor?.Title} {appt.Doctor?.User?.FullName}",
                AppointmentDate = appt.AppointmentDate,
                TimeSlot = appt.TimeSlot
            };

            return View(model);
        }

        // US-13: Cancel Appointment (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cancel(CancelAppointmentViewModel model)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int patientId))
            {
                return RedirectToAction("Login", "Account");
            }

            var appt = _context.Appointments.FirstOrDefault(a => a.Id == model.AppointmentId && a.PatientId == patientId);
            if (appt == null)
            {
                return NotFound();
            }

            if (!CanModifyAppointment(appt.AppointmentDate, appt.TimeSlot, appt.Status))
            {
                TempData["ErrorMessage"] = "Không thể hủy lịch vì đã quá hạn hủy (dưới 2 tiếng trước giờ khám)!";
                return RedirectToAction(nameof(Details), new { id = appt.Id });
            }

            // Free slot
            var schedule = _context.DoctorSchedules.FirstOrDefault(s =>
                s.DoctorId == appt.DoctorId &&
                s.WorkDate.Date == appt.AppointmentDate.Date &&
                s.TimeSlot == appt.TimeSlot);

            if (schedule != null)
            {
                schedule.BookedPatients = Math.Max(0, schedule.BookedPatients - 1);
                schedule.IsAvailable = true;
            }

            appt.Status = "Cancelled";
            appt.CancellationReason = model.CancellationReason;
            appt.CancelledAt = DateTime.UtcNow;
            appt.UpdatedAt = DateTime.UtcNow;

            _context.Notifications.Add(new Notification
            {
                UserId = patientId,
                Title = "Đã hủy lịch hẹn",
                Message = $"Bạn đã hủy thành công lịch hẹn {appt.AppointmentCode} ({appt.TimeSlot} ngày {appt.AppointmentDate:dd/MM/yyyy}).",
                Type = "StatusUpdate",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            _context.SaveChanges();

            TempData["SuccessMessage"] = "Đã hủy lịch hẹn thành công!";
            return RedirectToAction(nameof(MyAppointments));
        }

        // Notifications
        [HttpGet]
        public IActionResult Notifications()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var list = _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();

            // Mark all as read
            foreach (var item in list.Where(n => !n.IsRead))
            {
                item.IsRead = true;
            }
            _context.SaveChanges();

            return View(list);
        }

        // AJAX API: Get Doctors by Specialty
        [HttpGet]
        public IActionResult GetDoctorsBySpecialty(int specialtyId)
        {
            var doctors = _context.Doctors
                .Include(d => d.User)
                .Where(d => d.SpecialtyId == specialtyId)
                .Select(d => new
                {
                    id = d.Id,
                    name = d.User != null ? d.User.FullName : "Bác sĩ",
                    title = d.Title,
                    roomNumber = d.RoomNumber,
                    fee = d.ConsultationFee
                })
                .ToList();

            return Json(doctors);
        }

        // AJAX API: Get Available Slots
        [HttpGet]
        public IActionResult GetAvailableSlots(int doctorId, string date)
        {
            if (!DateTime.TryParse(date, out DateTime parsedDate))
            {
                return BadRequest("Ngày không hợp lệ");
            }

            var defaultSlots = new List<string>
            {
                "08:00 - 08:30", "08:30 - 09:00",
                "09:00 - 09:30", "09:30 - 10:00",
                "10:00 - 10:30", "10:30 - 11:00",
                "13:30 - 14:00", "14:00 - 14:30",
                "14:30 - 15:00", "15:00 - 15:30",
                "15:30 - 16:00", "16:00 - 16:30"
            };

            // Get booked slots for this doctor on this day
            var bookedSlots = _context.Appointments
                .Where(a => a.DoctorId == doctorId && a.AppointmentDate.Date == parsedDate.Date && a.Status != "Cancelled")
                .Select(a => a.TimeSlot)
                .ToHashSet();

            // Also check doctor schedules
            var unavailableSlots = _context.DoctorSchedules
                .Where(s => s.DoctorId == doctorId && s.WorkDate.Date == parsedDate.Date && !s.IsAvailable)
                .Select(s => s.TimeSlot)
                .ToHashSet();

            bookedSlots.UnionWith(unavailableSlots);

            var available = defaultSlots.Where(s => !bookedSlots.Contains(s)).ToList();

            return Json(available);
        }

        private bool CanModifyAppointment(DateTime apptDate, string timeSlot, string status)
        {
            if (status == "Cancelled" || status == "Completed")
            {
                return false;
            }

            // Extract start hour from timeSlot: e.g. "08:00 - 08:30" -> 08:00
            int startHour = 8, startMin = 0;
            if (!string.IsNullOrWhiteSpace(timeSlot))
            {
                var parts = timeSlot.Split('-');
                if (parts.Length > 0 && TimeSpan.TryParse(parts[0].Trim(), out var parsedTime))
                {
                    startHour = parsedTime.Hours;
                    startMin = parsedTime.Minutes;
                }
            }

            var appointmentDateTime = new DateTime(apptDate.Year, apptDate.Month, apptDate.Day, startHour, startMin, 0);
            var now = DateTime.Now;

            // Rule: Must be >= 2 hours prior to appointment
            return (appointmentDateTime - now).TotalHours >= 2;
        }
    }
}
