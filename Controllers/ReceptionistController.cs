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
            ViewBag.PendingContactCount = _context.ContactMessages.Count(c => !c.IsResolved);

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
        public IActionResult CreateAppointment(string? patientName, string? phone)
        {
            ViewBag.Specialties = _context.Specialties.Where(s => s.IsActive).ToList();
            ViewBag.Doctors = _context.Doctors.Include(d => d.User).ToList();
            ViewBag.PrefillPatientName = patientName;
            ViewBag.PrefillPhone = phone;
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

            // Kiểm tra không đặt lịch vào ngày trong quá khứ
            if (appointmentDate.Date < DateTime.Today)
            {
                TempData["ErrorMessage"] = "Không thể đặt lịch vào ngày trong quá khứ. Vui lòng chọn ngày từ hôm nay trở đi.";
                ViewBag.Specialties = _context.Specialties.Where(s => s.IsActive).ToList();
                ViewBag.Doctors = _context.Doctors.Include(d => d.User).ToList();
                return View();
            }

            // Kiểm tra không đặt lịch vào khung giờ đã trôi qua trong ngày hôm nay
            if (appointmentDate.Date == DateTime.Today && IsSlotInPast(timeSlot))
            {
                TempData["ErrorMessage"] = $"Khung giờ {timeSlot} đã trôi qua trong ngày hôm nay. Vui lòng chọn khung giờ khác.";
                ViewBag.Specialties = _context.Specialties.Where(s => s.IsActive).ToList();
                ViewBag.Doctors = _context.Doctors.Include(d => d.User).ToList();
                return View();
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
    
        // ==========================================
        // US18: Chuyển đổi lịch hẹn sang bác sĩ khác hoặc thời gian khác
        // ==========================================

        [HttpGet]
        public IActionResult TransferAppointment(int id)
        {
            var appt = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == id);

            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lịch hẹn yêu cầu.";
                return RedirectToAction(nameof(Appointments));
            }

            if (appt.Status == "Completed" || appt.Status == "Cancelled")
            {
                TempData["ErrorMessage"] = $"Không thể chuyển đổi lịch hẹn đã {(appt.Status == "Completed" ? "hoàn thành" : "bị hủy")}!";
                return RedirectToAction(nameof(Appointments));
            }

            var specialties = _context.Specialties
                .OrderBy(s => s.Name)
                .Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Name,
                    Selected = s.Id == appt.SpecialtyId
                })
                .ToList();

            var doctors = _context.Doctors
                .Include(d => d.User)
                .Where(d => d.SpecialtyId == appt.SpecialtyId && d.User != null)
                .Select(d => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = $"{d.Title} {d.User!.FullName} (Phòng {d.RoomNumber})",
                    Selected = d.Id == appt.DoctorId
                })
                .ToList();

            var defaultNewDate = appt.AppointmentDate.Date >= DateTime.Today ? appt.AppointmentDate.Date : DateTime.Today.AddDays(1);

            var model = new Healthy_System.Models.ViewModels.TransferAppointmentViewModel
            {
                AppointmentId = appt.Id,
                AppointmentCode = appt.AppointmentCode,
                PatientName = appt.Patient?.FullName ?? "Chưa rõ",
                PatientPhone = appt.Patient?.PhoneNumber,
                ReasonForVisit = appt.ReasonForVisit,
                CurrentStatus = appt.Status,

                CurrentDoctorId = appt.DoctorId,
                CurrentDoctorName = $"{appt.Doctor?.Title} {appt.Doctor?.User?.FullName}".Trim(),
                CurrentDoctorRoom = appt.Doctor?.RoomNumber,
                CurrentSpecialtyId = appt.SpecialtyId,
                CurrentSpecialtyName = appt.Specialty?.Name ?? string.Empty,
                CurrentDate = appt.AppointmentDate,
                CurrentTimeSlot = appt.TimeSlot,

                NewSpecialtyId = appt.SpecialtyId,
                NewDoctorId = appt.DoctorId,
                NewDate = defaultNewDate,
                NewTimeSlot = appt.TimeSlot,
                TransferReason = string.Empty,

                Specialties = specialties,
                Doctors = doctors
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TransferAppointment(Healthy_System.Models.ViewModels.TransferAppointmentViewModel model)
        {
            var appt = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == model.AppointmentId);

            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lịch hẹn.";
                return RedirectToAction(nameof(Appointments));
            }

            if (appt.Status == "Completed" || appt.Status == "Cancelled")
            {
                TempData["ErrorMessage"] = "Không thể chuyển đổi lịch hẹn đã hoàn thành hoặc đã bị hủy!";
                return RedirectToAction(nameof(Appointments));
            }

            if (model.NewDate.Date < DateTime.Today)
            {
                ModelState.AddModelError("NewDate", "Ngày khám mới không được ở trong quá khứ.");
            }

            if (model.NewDate.Date == DateTime.Today && IsSlotInPast(model.NewTimeSlot))
            {
                ModelState.AddModelError("NewTimeSlot", "Khung giờ này đã trôi qua trong ngày hôm nay. Vui lòng chọn khung giờ khác.");
            }

            var newDoctor = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .FirstOrDefault(d => d.Id == model.NewDoctorId);

            if (newDoctor == null)
            {
                ModelState.AddModelError("NewDoctorId", "Bác sĩ được chọn không tồn tại.");
            }

            if (ModelState.IsValid)
            {
                bool isConflict = _context.Appointments.Any(a =>
                    a.Id != model.AppointmentId &&
                    a.DoctorId == model.NewDoctorId &&
                    a.AppointmentDate.Date == model.NewDate.Date &&
                    a.TimeSlot == model.NewTimeSlot &&
                    a.Status != "Cancelled");

                if (isConflict)
                {
                    ModelState.AddModelError("NewTimeSlot", $"Bác sĩ {newDoctor?.User?.FullName} đã có lịch hẹn vào khung giờ {model.NewTimeSlot} ngày {model.NewDate:dd/MM/yyyy}. Vui lòng chọn giờ khác.");
                }

                bool isUnavailable = _context.DoctorSchedules.Any(s =>
                    s.DoctorId == model.NewDoctorId &&
                    s.WorkDate.Date == model.NewDate.Date &&
                    s.TimeSlot == model.NewTimeSlot &&
                    !s.IsAvailable);

                if (isUnavailable)
                {
                    ModelState.AddModelError("NewTimeSlot", "Bác sĩ không nhận lịch vào khung giờ này theo lịch trực. Vui lòng chọn giờ khác.");
                }
            }

            if (!ModelState.IsValid)
            {
                model.Specialties = _context.Specialties
                    .OrderBy(s => s.Name)
                    .Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                    {
                        Value = s.Id.ToString(),
                        Text = s.Name,
                        Selected = s.Id == model.NewSpecialtyId
                    })
                    .ToList();

                model.Doctors = _context.Doctors
                    .Include(d => d.User)
                    .Where(d => d.SpecialtyId == model.NewSpecialtyId && d.User != null)
                    .Select(d => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                    {
                        Value = d.Id.ToString(),
                        Text = $"{d.Title} {d.User!.FullName} (Phòng {d.RoomNumber})",
                        Selected = d.Id == model.NewDoctorId
                    })
                    .ToList();

                model.PatientName = appt.Patient?.FullName ?? "Chưa rõ";
                model.PatientPhone = appt.Patient?.PhoneNumber;
                model.AppointmentCode = appt.AppointmentCode;
                model.CurrentDoctorName = $"{appt.Doctor?.Title} {appt.Doctor?.User?.FullName}".Trim();
                model.CurrentSpecialtyName = appt.Specialty?.Name ?? string.Empty;
                model.CurrentDate = appt.AppointmentDate;
                model.CurrentTimeSlot = appt.TimeSlot;

                return View(model);
            }

            var oldDoctorName = $"{appt.Doctor?.Title} {appt.Doctor?.User?.FullName}".Trim();
            var newDoctorName = $"{newDoctor?.Title} {newDoctor?.User?.FullName}".Trim();
            var oldDateStr = appt.AppointmentDate.ToString("dd/MM/yyyy");
            var newDateStr = model.NewDate.ToString("dd/MM/yyyy");
            var patientName = appt.Patient?.FullName ?? "Bệnh nhân";

            string transferLog = $"[{DateTime.Now:dd/MM/yyyy HH:mm} - Tiếp tân chuyển đổi]: Lý do: {model.TransferReason.Trim()}. " +
                                 $"Từ [BS {oldDoctorName} - {oldDateStr} ({appt.TimeSlot})] sang [BS {newDoctorName} - {newDateStr} ({model.NewTimeSlot})]";

            appt.DoctorNotes = string.IsNullOrWhiteSpace(appt.DoctorNotes) 
                ? transferLog 
                : $"{appt.DoctorNotes}\n{transferLog}";

            var oldDoctorUserId = appt.Doctor?.UserId;
            var isDoctorChanged = appt.DoctorId != model.NewDoctorId;

            appt.DoctorId = model.NewDoctorId;
            appt.SpecialtyId = model.NewSpecialtyId;
            appt.AppointmentDate = model.NewDate.Date;
            appt.TimeSlot = model.NewTimeSlot;
            appt.UpdatedAt = DateTime.UtcNow;

            if (appt.Status == "Expired")
            {
                appt.Status = "Confirmed";
            }

            if (model.NotifyPatient && appt.PatientId > 0)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = appt.PatientId,
                    Title = "Thông báo điều chỉnh lịch khám",
                    Message = $"Lịch khám {appt.AppointmentCode} của bạn đã được tiếp tân chuyển sang Bác sĩ {newDoctorName} lúc {model.NewTimeSlot} ngày {newDateStr}. Lý do: {model.TransferReason.Trim()}.",
                    Type = "StatusUpdate",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (model.NotifyDoctors)
            {
                if (newDoctor?.UserId != null)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = newDoctor.UserId,
                        Title = "Lịch khám mới được điều chuyển đến bạn",
                        Message = $"Lịch khám {appt.AppointmentCode} của bệnh nhân {patientName} đã được tiếp tân chuyển đến bạn vào ngày {newDateStr} ({model.NewTimeSlot}). Lý do: {model.TransferReason.Trim()}.",
                        Type = "StatusUpdate",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (isDoctorChanged && oldDoctorUserId != null)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = oldDoctorUserId.Value,
                        Title = "Lịch khám đã chuyển sang bác sĩ khác",
                        Message = $"Lịch khám {appt.AppointmentCode} (bệnh nhân {patientName}, ngày {oldDateStr} {appt.TimeSlot}) đã được tiếp tân chuyển sang Bác sĩ {newDoctorName}. Lý do: {model.TransferReason.Trim()}.",
                        Type = "StatusUpdate",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã chuyển đổi thành công lịch hẹn {appt.AppointmentCode} sang BS {newDoctorName} ({newDateStr} lúc {model.NewTimeSlot})!";
            return RedirectToAction(nameof(Appointments));
        }

        [HttpGet]
        public IActionResult GetAvailableSlotsForTransfer(int doctorId, string date, int? currentAppointmentId)
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

            var bookedQuery = _context.Appointments
                .Where(a => a.DoctorId == doctorId && a.AppointmentDate.Date == parsedDate.Date && a.Status != "Cancelled");

            if (currentAppointmentId.HasValue)
            {
                bookedQuery = bookedQuery.Where(a => a.Id != currentAppointmentId.Value);
            }

            var bookedSlots = bookedQuery.Select(a => a.TimeSlot).ToHashSet();

            var unavailableSlots = _context.DoctorSchedules
                .Where(s => s.DoctorId == doctorId && s.WorkDate.Date == parsedDate.Date && !s.IsAvailable)
                .Select(s => s.TimeSlot)
                .ToHashSet();

            bookedSlots.UnionWith(unavailableSlots);

            var available = defaultSlots.Where(s => !bookedSlots.Contains(s)).ToList();

            if (parsedDate.Date == DateTime.Today)
            {
                available = available.Where(s => !IsSlotInPast(s)).ToList();
            }

            return Json(available);
        }

        private static bool IsSlotInPast(string timeSlot)
        {
            if (string.IsNullOrWhiteSpace(timeSlot)) return false;
            var parts = timeSlot.Split('-');
            if (parts.Length > 0 && TimeSpan.TryParse(parts[0].Trim(), out var startTime))
            {
                return DateTime.Now.TimeOfDay > startTime;
            }
            return false;
        }

        // US-07 / US-Reception: Quản lý yêu cầu liên hệ từ người dùng
        [HttpGet]
        public IActionResult ContactRequests(string? status, string? search)
        {
            var query = _context.ContactMessages.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status.Equals("pending", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(c => !c.IsResolved);
                }
                else if (status.Equals("resolved", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.IsResolved);
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(c => c.FullName.ToLower().Contains(term)
                                      || (c.Phone != null && c.Phone.Contains(term))
                                      || c.Email.ToLower().Contains(term)
                                      || c.Subject.ToLower().Contains(term)
                                      || c.Message.ToLower().Contains(term));
            }

            ViewBag.SelectedStatus = status ?? "all";
            ViewBag.SearchTerm = search;
            ViewBag.TotalCount = _context.ContactMessages.Count();
            ViewBag.PendingCount = _context.ContactMessages.Count(c => !c.IsResolved);
            ViewBag.ResolvedCount = _context.ContactMessages.Count(c => c.IsResolved);

            var list = query
                .OrderBy(c => c.IsResolved)
                .ThenByDescending(c => c.CreatedAt)
                .ToList();

            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResolveContact(int id, string? returnStatus, string? returnSearch)
        {
            var contact = _context.ContactMessages.FirstOrDefault(c => c.Id == id);
            if (contact == null) return NotFound();

            contact.IsResolved = true;
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã cập nhật yêu cầu liên hệ từ bệnh nhân \"{contact.FullName}\" sang trạng thái Đã xử lý!";
            return RedirectToAction(nameof(ContactRequests), new { status = returnStatus, search = returnSearch });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UnresolveContact(int id, string? returnStatus, string? returnSearch)
        {
            var contact = _context.ContactMessages.FirstOrDefault(c => c.Id == id);
            if (contact == null) return NotFound();

            contact.IsResolved = false;
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã chuyển lại yêu cầu liên hệ từ bệnh nhân \"{contact.FullName}\" sang Chưa xử lý.";
            return RedirectToAction(nameof(ContactRequests), new { status = returnStatus, search = returnSearch });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteContact(int id, string? returnStatus, string? returnSearch)
        {
            var contact = _context.ContactMessages.FirstOrDefault(c => c.Id == id);
            if (contact == null) return NotFound();

            _context.ContactMessages.Remove(contact);
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã xóa yêu cầu liên hệ của \"{contact.FullName}\"!";
            return RedirectToAction(nameof(ContactRequests), new { status = returnStatus, search = returnSearch });
        }
}
}
