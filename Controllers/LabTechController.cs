using Healthy_System.Data;
using Healthy_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;

namespace Healthy_System.Controllers
{
    // US29: Nhap ket qua xet nghiem / sieu am / X-quang
    // US30: Dinh kem file hinh anh
    [Authorize(Roles = "LabTech")]
    public class LabTechController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public LabTechController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private int GetCurrentUserId()
        {
            var str = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(str, out int id) ? id : 0;
        }

        // =====================================================================
        // US29: Danh sach phieu yeu cau XN can xu ly
        // =====================================================================
        public IActionResult Index(string? search, string? filter)
        {
            // Lay tat ca appointment co LabRequest (bac si da lap phieu)
            var query = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Specialty)
                .Where(a => !string.IsNullOrWhiteSpace(a.LabRequest))
                .AsQueryable();

            // Tim kiem theo ten BN hoac ma lich hen
            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim().ToLower();
                query = query.Where(a =>
                    (a.Patient != null && a.Patient.FullName.ToLower().Contains(kw)) ||
                    a.AppointmentCode.ToLower().Contains(kw));
            }

            // Loc theo trang thai: pending (chua co ket qua) / done (da co ket qua)
            if (filter == "pending")
            {
                var doneIds = _context.LabResults.Select(r => r.AppointmentId).ToList();
                query = query.Where(a => !doneIds.Contains(a.Id));
            }
            else if (filter == "done")
            {
                var doneIds = _context.LabResults.Select(r => r.AppointmentId).ToList();
                query = query.Where(a => doneIds.Contains(a.Id));
            }

            var list = query.OrderByDescending(a => a.AppointmentDate).ThenBy(a => a.TimeSlot).ToList();

            // Lay danh sach da co ket qua de check
            var enteredIds = _context.LabResults.Select(r => r.AppointmentId).ToList();

            ViewBag.Search = search;
            ViewBag.Filter = filter;
            ViewBag.EnteredIds = enteredIds;
            ViewBag.TotalPending = list.Count(a => !enteredIds.Contains(a.Id));
            ViewBag.TotalDone = list.Count(a => enteredIds.Contains(a.Id));

            return View(list);
        }

        // =====================================================================
        // US29: Form nhap ket qua XN
        // =====================================================================
        [HttpGet]
        public IActionResult EnterResult(int id)
        {
            var appointment = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Specialty)
                .FirstOrDefault(a => a.Id == id);

            if (appointment == null) return NotFound();

            // Neu da co ket qua roi -> load len de xem/sua
            var existing = _context.LabResults
                .Include(r => r.LabTech)
                .FirstOrDefault(r => r.AppointmentId == id);

            ViewBag.ExistingResult = existing;
            return View(appointment);
        }

        // =====================================================================
        // US29 + US30: Luu ket qua XN (co the dinh kem file)
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveResult(
            int appointmentId,
            string resultSummary,
            string? detailedResult,
            string? labTechNotes,
            IFormFile? imageFile) // US30: file dinh kem
        {
            if (string.IsNullOrWhiteSpace(resultSummary))
            {
                TempData["ErrorMessage"] = "Vui long nhap ket qua xet nghiem.";
                return RedirectToAction(nameof(EnterResult), new { id = appointmentId });
            }

            var labTechId = GetCurrentUserId();
            if (labTechId == 0) return RedirectToAction("Login", "Account");

            var appointment = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .FirstOrDefault(a => a.Id == appointmentId);

            if (appointment == null) return NotFound();

            // Kiem tra da co ket qua chua (neu co -> cap nhat, chua -> tao moi)
            var existing = _context.LabResults.FirstOrDefault(r => r.AppointmentId == appointmentId);
            string? savedFilePath = existing?.ImageFilePath;
            string? savedFileName = existing?.ImageFileName;
            string? savedFileType = existing?.ImageFileType;

            // US30: Xu ly upload file hinh anh neu co
            if (imageFile != null && imageFile.Length > 0)
            {
                // Chi cho phep: jpg, jpeg, png, pdf, dcm
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".dcm" };
                var ext = Path.GetExtension(imageFile.FileName).ToLower();

                if (!allowedExtensions.Contains(ext))
                {
                    TempData["ErrorMessage"] = "Chi chap nhan file anh (JPG, PNG), PDF hoac DICOM (.dcm).";
                    return RedirectToAction(nameof(EnterResult), new { id = appointmentId });
                }

                if (imageFile.Length > 10 * 1024 * 1024) // Gioi han 10MB
                {
                    TempData["ErrorMessage"] = "File qua lon. Vui long chon file duoi 10MB.";
                    return RedirectToAction(nameof(EnterResult), new { id = appointmentId });
                }

                // Tao thu muc luu file
                var uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "labresults");
                Directory.CreateDirectory(uploadFolder);

                // Tao ten file duy nhat
                var uniqueName = $"LAB-{appointmentId}-{DateTime.Now:yyyyMMddHHmmss}{ext}";
                var fullPath = Path.Combine(uploadFolder, uniqueName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    imageFile.CopyTo(stream);
                }

                savedFilePath = $"/uploads/labresults/{uniqueName}";
                savedFileName = imageFile.FileName;
                savedFileType = ext.TrimStart('.');
            }

            if (existing != null)
            {
                // Cap nhat ket qua cu
                existing.ResultSummary = resultSummary.Trim();
                existing.DetailedResult = detailedResult?.Trim();
                existing.LabTechNotes = labTechNotes?.Trim();
                existing.ImageFilePath = savedFilePath;
                existing.ImageFileName = savedFileName;
                existing.ImageFileType = savedFileType;
                existing.UpdatedAt = DateTime.UtcNow;
                TempData["SuccessMessage"] = $"Da cap nhat ket qua xet nghiem cho benh nhan {appointment.Patient?.FullName}!";
            }
            else
            {
                // Tao moi ket qua
                var result = new LabResult
                {
                    AppointmentId = appointmentId,
                    LabTechId = labTechId,
                    ResultSummary = resultSummary.Trim(),
                    DetailedResult = detailedResult?.Trim(),
                    LabTechNotes = labTechNotes?.Trim(),
                    ImageFilePath = savedFilePath,
                    ImageFileName = savedFileName,
                    ImageFileType = savedFileType,
                    Status = "Completed",
                    ResultDate = DateTime.UtcNow
                };
                _context.LabResults.Add(result);

                // US29: Gui thong bao cho bac si biet co ket qua moi
                if (appointment.Doctor?.UserId != null)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = appointment.Doctor.UserId,
                        Title = "KQ Xet Nghiem Da Co",
                        Message = $"Ket qua xet nghiem cho benh nhan {appointment.Patient?.FullName} (lich hen {appointment.AppointmentCode}) da duoc ky thuat vien cap nhat. Vao muc Ho So Kham Benh de xem chi tiet.",
                        Type = "LabResult",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // Gui thong bao cho benh nhan
                _context.Notifications.Add(new Notification
                {
                    UserId = appointment.PatientId,
                    Title = "Ket Qua Xet Nghiem Cua Ban Da Co",
                    Message = $"Ket qua xet nghiem cho lich hen {appointment.AppointmentCode} da duoc cap nhat. Bac si se xem xet va thong bao them cho ban.",
                    Type = "LabResult",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });

                TempData["SuccessMessage"] = $"Da luu ket qua xet nghiem thanh cong cho benh nhan {appointment.Patient?.FullName}!";
            }

            _context.SaveChanges();
            return RedirectToAction(nameof(EnterResult), new { id = appointmentId });
        }

        // =====================================================================
        // Xem chi tiet ket qua da nhap
        // =====================================================================
        public IActionResult ViewResult(int id)
        {
            var result = _context.LabResults
                .Include(r => r.Appointment).ThenInclude(a => a!.Patient)
                .Include(r => r.Appointment).ThenInclude(a => a!.Doctor).ThenInclude(d => d!.User)
                .Include(r => r.Appointment).ThenInclude(a => a!.Specialty)
                .Include(r => r.LabTech)
                .FirstOrDefault(r => r.Id == id);

            if (result == null) return NotFound();
            return View(result);
        }
    }
}
