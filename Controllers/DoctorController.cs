using Healthy_System.Data;
using Healthy_System.Models;
using Healthy_System.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;

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

        [AllowAnonymous]
        public IActionResult Unconfigured()
        {
            return View();
        }

        // US-30: View Doctor Dashboard & Queue
        public IActionResult Dashboard()
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null)
            {
                return RedirectToAction(nameof(Unconfigured));
            }

            AutoUpdateOverdueAppointments(doctor.Id);

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
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            AutoUpdateOverdueAppointments(doctor.Id);

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

        // Tự động rà soát và cập nhật trạng thái các lịch hẹn quá hạn ngày khám
        private void AutoUpdateOverdueAppointments(int? doctorId = null)
        {
            try
            {
                var query = _context.Appointments
                    .Where(a => a.Status == "Pending" || a.Status == "Confirmed");

                if (doctorId.HasValue)
                {
                    query = query.Where(a => a.DoctorId == doctorId.Value);
                }

                var candidates = query.ToList();
                var today = DateTime.Today;
                var overdue = candidates.Where(a => a.AppointmentDate.Date < today).ToList();

                if (overdue.Any())
                {
                    foreach (var appt in overdue)
                    {
                        appt.Status = "Expired";
                        appt.UpdatedAt = DateTime.UtcNow;
                    }
                    _context.SaveChanges();
                }
            }
            catch
            {
                // Bỏ qua ngoại lệ nếu có xung đột truy cập đồng thời
            }
        }

        // US-31 & US-34: Clinical Examination & e-Prescription
        public IActionResult Examination(int id)
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            var appointment = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == id && a.DoctorId == doctor.Id);

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lịch hẹn hoặc bạn không phụ trách ca khám này.";
                return RedirectToAction(nameof(Queue));
            }

            // Nếu ca khám đã hoàn thành -> Chuyển hướng sang xem hồ sơ bệnh án, không cho khám lại đè dữ liệu
            if (appointment.Status == "Completed")
            {
                TempData["InfoMessage"] = "Ca khám này đã hoàn tất chẩn đoán. Chuyển hướng đến hồ sơ bệnh án.";
                return RedirectToAction(nameof(MedicalRecord), new { id = appointment.Id });
            }

            // Nếu ca khám đã bị hủy
            if (appointment.Status == "Cancelled")
            {
                TempData["ErrorMessage"] = "Lịch hẹn này đã bị hủy, không thể tiến hành khám bệnh.";
                return RedirectToAction(nameof(PatientList));
            }

            // Nếu ca khám đã quá hạn (ngày hẹn trong quá khứ mà chưa khám)
            if (appointment.Status == "Expired" || appointment.AppointmentDate.Date < DateTime.Today)
            {
                TempData["ErrorMessage"] = $"Lịch hẹn của bệnh nhân {appointment.Patient?.FullName} đã quá hạn ngày khám ({appointment.AppointmentDate:dd/MM/yyyy}).";
                return RedirectToAction(nameof(PatientList));
            }

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
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            var appt = _context.Appointments.Include(a => a.Patient).FirstOrDefault(a => a.Id == appointmentId && a.DoctorId == doctor.Id);
            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ca khám hoặc bạn không phụ trách ca khám này.";
                return RedirectToAction(nameof(Queue));
            }

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
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            var appt = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .FirstOrDefault(a => a.Id == appointmentId && a.DoctorId == doctor.Id);

            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ca khám hoặc bạn không phụ trách ca khám này.";
                return RedirectToAction(nameof(Queue));
            }

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
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            var appt = _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefault(a => a.Id == appointmentId && a.DoctorId == doctor.Id);

            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ca khám hoặc bạn không phụ trách ca khám này.";
                return RedirectToAction(nameof(Queue));
            }

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
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

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
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            var appt = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Doctor).ThenInclude(d => d!.Specialty)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == id && a.DoctorId == doctor.Id);

            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ hoặc bạn không phụ trách ca khám này.";
                return RedirectToAction(nameof(MedicalRecords));
            }

            ViewBag.Doctor = doctor;
            ViewBag.HasLabResult = _context.LabResults.Any(r => r.AppointmentId == id);
            return View(appt);
        }

        // US-35: Doctor Work Schedule
        public IActionResult Schedule()
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

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
        [HttpGet]
        public IActionResult Profile()
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            var model = new DoctorProfileViewModel
            {
                Id = doctor.Id,
                FullName = doctor.User?.FullName ?? string.Empty,
                Email = doctor.User?.Email ?? string.Empty,
                PhoneNumber = doctor.User?.PhoneNumber,
                AvatarUrl = doctor.User?.AvatarUrl,
                Title = doctor.Title,
                SpecialtyId = doctor.SpecialtyId,
                ExperienceYears = doctor.ExperienceYears,
                RoomNumber = doctor.RoomNumber,
                ConsultationFee = doctor.ConsultationFee,
                Bio = doctor.Bio,
                Specialties = _context.Specialties.Where(s => s.IsActive).OrderBy(s => s.Name).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Profile(DoctorProfileViewModel model)
        {
            var currentDoctor = GetCurrentDoctor();
            if (currentDoctor == null || currentDoctor.Id != model.Id)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                model.Specialties = _context.Specialties.Where(s => s.IsActive).OrderBy(s => s.Name).ToList();
                return View(model);
            }

            var doctor = _context.Doctors.Include(d => d.User).FirstOrDefault(d => d.Id == model.Id);
            if (doctor == null) return NotFound();

            // Cập nhật thông tin tài khoản User
            if (doctor.User != null)
            {
                doctor.User.FullName = model.FullName.Trim();
                doctor.User.PhoneNumber = model.PhoneNumber?.Trim();
                if (!string.IsNullOrWhiteSpace(model.AvatarUrl))
                {
                    doctor.User.AvatarUrl = model.AvatarUrl.Trim();
                }
            }

            // Cập nhật thông tin Chuyên môn Doctor
            doctor.Title = model.Title.Trim();
            doctor.SpecialtyId = model.SpecialtyId;
            doctor.ExperienceYears = model.ExperienceYears;
            doctor.RoomNumber = model.RoomNumber.Trim();
            doctor.ConsultationFee = model.ConsultationFee;
            doctor.Bio = model.Bio?.Trim() ?? string.Empty;

            _context.SaveChanges();

            TempData["SuccessMessage"] = "Cập nhật hồ sơ cá nhân & chuyên môn thành công!";
            return RedirectToAction(nameof(Profile));
        }

        // POST: Doctor/AddSchedule - Thêm ca làm việc mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddSchedule(DateTime workDate, string timeSlot, int maxPatients = 1, string? notes = null)
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            if (workDate.Date < DateTime.Today)
            {
                TempData["ErrorMessage"] = "Không thể đăng ký lịch trực cho ngày trong quá khứ.";
                return RedirectToAction(nameof(Schedule));
            }

            // Kiểm tra trùng lịch
            bool exists = _context.DoctorSchedules.Any(s =>
                s.DoctorId == doctor.Id &&
                s.WorkDate.Date == workDate.Date &&
                s.TimeSlot == timeSlot);

            if (exists)
            {
                TempData["ErrorMessage"] = $"Khung giờ {timeSlot} ngày {workDate:dd/MM/yyyy} đã tồn tại trong lịch trình.";
                return RedirectToAction(nameof(Schedule));
            }

            var schedule = new DoctorSchedule
            {
                DoctorId = doctor.Id,
                WorkDate = workDate.Date,
                TimeSlot = timeSlot,
                MaxPatients = maxPatients,
                BookedPatients = 0,
                IsAvailable = true,
                Notes = notes?.Trim()
            };

            _context.DoctorSchedules.Add(schedule);
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Thêm ca trực {timeSlot} ngày {workDate:dd/MM/yyyy} thành công!";
            return RedirectToAction(nameof(Schedule));
        }

        // POST: Doctor/ToggleScheduleStatus - Bật/Tắt (Hủy) ca trực
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleScheduleStatus(int id)
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            var schedule = _context.DoctorSchedules.FirstOrDefault(s => s.Id == id && s.DoctorId == doctor.Id);
            if (schedule == null) return NotFound();

            // Nếu đã có bệnh nhân đặt, không cho phép tắt trực tiếp
            if (schedule.BookedPatients > 0 && schedule.IsAvailable)
            {
                TempData["ErrorMessage"] = "Ca trực này đã có bệnh nhân đặt lịch, không thể hủy ngang. Vui lòng xử lý ở Hàng đợi!";
                return RedirectToAction(nameof(Schedule));
            }

            schedule.IsAvailable = !schedule.IsAvailable;
            _context.SaveChanges();

            string statusStr = schedule.IsAvailable ? "Mở nhận bệnh" : "Tắt / Tạm dừng";
            TempData["SuccessMessage"] = $"Đã cập nhật ca trực {schedule.TimeSlot} sang trạng thái: {statusStr}";
            return RedirectToAction(nameof(Schedule));
        }

        // =====================================================================
        // US20: Gửi thông báo / liên hệ bệnh nhân khi cần
        // =====================================================================

        [HttpGet]
        public IActionResult SendNotification(int appointmentId)
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            var appointment = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == appointmentId && a.DoctorId == doctor.Id);

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lịch hẹn hoặc bạn không phụ trách ca khám này.";
                return RedirectToAction(nameof(PatientList));
            }

            ViewBag.Doctor = doctor;
            ViewBag.Appointment = appointment;
            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendNotification(int appointmentId, string notificationType, string notificationTitle, string notificationMessage)
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            var appointment = _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefault(a => a.Id == appointmentId && a.DoctorId == doctor.Id);

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lịch hẹn hoặc bạn không phụ trách ca khám này.";
                return RedirectToAction(nameof(PatientList));
            }

            if (string.IsNullOrWhiteSpace(notificationMessage))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập nội dung thông báo.";
                return RedirectToAction(nameof(SendNotification), new { appointmentId });
            }

            var doctorName = doctor.User?.FullName ?? "Bác sĩ phụ trách";

            _context.Notifications.Add(new Notification
            {
                UserId = appointment.PatientId,
                Title = string.IsNullOrWhiteSpace(notificationTitle)
                    ? $"📢 Thông báo từ BS. {doctorName}"
                    : notificationTitle.Trim(),
                Message = $"[BS. {doctorName}]: {notificationMessage.Trim()}",
                Type = notificationType ?? "General",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã gửi thông báo thành công đến bệnh nhân {appointment.Patient?.FullName}!";
            return RedirectToAction(nameof(PatientList));
        }

        // =====================================================================
        // US21: Xem danh sách và hồ sơ bệnh nhân cần khám
        // =====================================================================

        [HttpGet]
        public IActionResult PatientList(string? search, string? status, string? dateFilter)
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            // Tự động quét và cập nhật trạng thái các ca quá hạn khám của bác sĩ
            AutoUpdateOverdueAppointments(doctor.Id);

            var query = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Specialty)
                .Where(a => a.DoctorId == doctor.Id)
                .AsQueryable();

            // Lọc theo từ khóa tìm kiếm
            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim().ToLower();
                query = query.Where(a =>
                    (a.Patient != null && a.Patient.FullName.ToLower().Contains(kw)) ||
                    a.AppointmentCode.ToLower().Contains(kw) ||
                    (a.Patient != null && a.Patient.PhoneNumber != null && a.Patient.PhoneNumber.Contains(kw)));
            }

            // Lọc theo trạng thái
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status == status);
            }

            // Lọc theo ngày
            if (dateFilter == "today")
            {
                var today = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);
                query = query.Where(a => a.AppointmentDate.Date == today);
            }
            else if (dateFilter == "upcoming")
            {
                var today = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);
                query = query.Where(a => a.AppointmentDate.Date >= today && a.Status != "Cancelled" && a.Status != "Completed");
            }

            var list = query
                .OrderByDescending(a => a.AppointmentDate)
                .ThenBy(a => a.TimeSlot)
                .ToList();

            ViewBag.Doctor = doctor;
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.DateFilter = dateFilter;
            ViewBag.TotalCount = list.Count;
            ViewBag.PendingCount = list.Count(a => a.Status == "Pending" || a.Status == "Confirmed");
            ViewBag.CompletedCount = list.Count(a => a.Status == "Completed");

            // US23: Lay danh sach appointmentId da co LabRequest va da co LabResult
            var apptIds = list.Select(a => a.Id).ToList();
            ViewBag.LabRequestIds = list.Where(a => !string.IsNullOrWhiteSpace(a.LabRequest)).Select(a => a.Id).ToList();
            ViewBag.LabResultIds = _context.LabResults.Where(r => apptIds.Contains(r.AppointmentId)).Select(r => r.AppointmentId).ToList();
            return View(list);
        }

        [HttpGet]
        public IActionResult PatientProfile(int appointmentId)
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            var appointment = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Specialty)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .FirstOrDefault(a => a.Id == appointmentId && a.DoctorId == doctor.Id);

            if (appointment == null) return NotFound();

            // Lịch sử khám trước đây của bệnh nhân này
            var history = _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Specialty)
                .Where(a => a.PatientId == appointment.PatientId && a.Id != appointmentId)
                .OrderByDescending(a => a.AppointmentDate)
                .ToList();

            ViewBag.Doctor = doctor;
            ViewBag.History = history;
            return View(appointment);
        }

        // =====================================================================
        // US22: Lập phiếu yêu cầu xét nghiệm / chụp chiếu
        // =====================================================================

        [HttpGet]
        public IActionResult LabRequestForm(int id)
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            var appointment = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == id && a.DoctorId == doctor.Id);

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ca khám hoặc bạn không phụ trách ca khám này.";
                return RedirectToAction(nameof(PatientList));
            }

            ViewBag.Doctor = doctor;
            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveLabRequest(int appointmentId, string labRequest)
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            var appt = _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefault(a => a.Id == appointmentId && a.DoctorId == doctor.Id);

            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ca khám hoặc bạn không phụ trách ca khám này.";
                return RedirectToAction(nameof(PatientList));
            }

            // Validation: phai co noi dung
            if (string.IsNullOrWhiteSpace(labRequest))
            {
                TempData["ErrorMessage"] = "Vui long nhap noi dung phieu xet nghiem truoc khi luu.";
                return RedirectToAction(nameof(LabRequestForm), new { id = appointmentId });
            }

            appt.LabRequest = labRequest.Trim();
            appt.UpdatedAt = DateTime.UtcNow;

            // Gui thong bao cho benh nhan
            _context.Notifications.Add(new Notification
            {
                UserId = appt.PatientId,
                Title = "Phieu Yeu Cau Xet Nghiem",
                Message = $"Bac si da lap phieu xet nghiem cho lich hen {appt.AppointmentCode}. KTV se xu ly va thong bao ket qua.",
                Type = "LabResult",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            // Gui thong bao cho tat ca KTV
            var labTechs = _context.Users.Where(u => u.Role == "LabTech").ToList();
            foreach (var ktv in labTechs)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = ktv.Id,
                    Title = "Co Phieu XN Moi Can Xu Ly",
                    Message = $"BS {doctor.User?.FullName} vua lap phieu XN cho BN {appt.Patient?.FullName} (ma: {appt.AppointmentCode}). Vao Phieu Yeu Cau XN de xu ly.",
                    Type = "LabResult",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Da luu phieu XN cho BN {appt.Patient?.FullName}! Da thong bao KTV va benh nhan.";
            return RedirectToAction(nameof(LabRequestForm), new { id = appointmentId });

        }
        // =====================================================================
        // =====================================================================
        // US23: Bac si xem ket qua xet nghiem
        // =====================================================================
        [HttpGet]
        public IActionResult LabResultView(int appointmentId)
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            // Lay ket qua XN cua appointment nay
            var result = _context.LabResults
                .Include(r => r.Appointment).ThenInclude(a => a!.Patient)
                .Include(r => r.Appointment).ThenInclude(a => a!.Specialty)
                .Include(r => r.LabTech)
                .FirstOrDefault(r => r.AppointmentId == appointmentId && r.Appointment!.DoctorId == doctor.Id);

            if (result == null)
            {
                TempData["ErrorMessage"] = "Chua co ket qua xet nghiem cho ca kham nay.";
                return RedirectToAction(nameof(MedicalRecord), new { id = appointmentId });
            }

            return View(result);
        }

        // US24: Kê đơn thuốc điện tử cho bệnh nhân
        // =====================================================================

        [HttpGet]
        public IActionResult PrescriptionForm(int id)
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            var appointment = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == id && a.DoctorId == doctor.Id);

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ca khám hoặc bạn không phụ trách ca khám này.";
                return RedirectToAction(nameof(PatientList));
            }

            ViewBag.Doctor = doctor;
            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SavePrescription(int appointmentId, string prescription, string? diagnosis, string? doctorNotes)
        {
            var doctor = GetCurrentDoctor();
            if (doctor == null) return RedirectToAction(nameof(Unconfigured));

            var appt = _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefault(a => a.Id == appointmentId && a.DoctorId == doctor.Id);

            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ca khám hoặc bạn không phụ trách ca khám này.";
                return RedirectToAction(nameof(PatientList));
            }

            if (string.IsNullOrWhiteSpace(prescription))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập ít nhất 1 loại thuốc vào đơn.";
                return RedirectToAction(nameof(PrescriptionForm), new { id = appointmentId });
            }

            appt.Prescription = prescription.Trim();
            if (!string.IsNullOrWhiteSpace(diagnosis))
                appt.Diagnosis = diagnosis.Trim();
            if (!string.IsNullOrWhiteSpace(doctorNotes))
                appt.DoctorNotes = doctorNotes.Trim();
            appt.UpdatedAt = DateTime.UtcNow;

            // Gửi thông báo đơn thuốc cho bệnh nhân
            _context.Notifications.Add(new Notification
            {
                UserId = appt.PatientId,
                Title = "💊 Đơn Thuốc Điện Tử Từ Bác Sĩ",
                Message = $"Bác sĩ đã kê đơn thuốc điện tử cho lịch hẹn {appt.AppointmentCode}. Bạn có thể xem bằng cách vào mục \"Lịch hẹn của tôi\" → tìm lịch hẹn {appt.AppointmentCode} → nhấn nút \"Xem Đơn Thuốc\" (màu xanh lá).",
                Type = "StatusUpdate",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã kê đơn thuốc điện tử thành công cho bệnh nhân {appt.Patient?.FullName}!";
            return RedirectToAction(nameof(PrescriptionForm), new { id = appointmentId });
        }
    }
}
