using System;
using System.Linq;
using System.Security.Claims;
using BCrypt.Net;
using Healthy_System.Data;
using Healthy_System.Models;
using Healthy_System.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Healthy_System.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // US35 â€” Quáº£n lÃ½ tÃ i khoáº£n & phÃ¢n quyá»n
        public IActionResult Dashboard()
        {
            ViewBag.TotalUsers = _context.Users.Count();
            ViewBag.TotalPatients = _context.Users.Count(u => u.Role == "Patient");
            ViewBag.TotalDoctors = _context.Doctors.Count();
            ViewBag.TotalAppointments = _context.Appointments.Count();
            ViewBag.TotalEquipments = _context.Equipments.Count();

            var users = _context.Users.OrderByDescending(u => u.CreatedAt).ToList();
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangeRole(int userId, string newRole)
        {
            // RBAC Rule: Chá»‰ cho phÃ©p gÃ¡n cÃ¡c vai trÃ² nhÃ¢n sá»± thÃ´ng thÆ°á»ng qua UI.
            // - "Admin" : KHÃ”NG cho phÃ©p gÃ¡n qua UI â€” Admin chá»‰ táº¡o qua DB seed ban Ä‘áº§u.
            // - "Doctor": KHÃ”NG cho phÃ©p gÃ¡n qua UI â€” pháº£i qua flow "Tiáº¿p Nháº­n BÃ¡c SÄ©".
            var allowedRoles = new[] { "Receptionist", "LabTech", "Accountant", "Patient" };
            if (!allowedRoles.Contains(newRole))
            {
                TempData["ErrorMessage"] = newRole == "Admin"
                    ? "KhÃ´ng thá»ƒ cáº¥p quyá»n Quáº£n Trá»‹ ViÃªn qua giao diá»‡n. LiÃªn há»‡ quáº£n trá»‹ há»‡ thá»‘ng náº¿u cáº§n táº¡o Admin má»›i."
                    : newRole == "Doctor"
                        ? "KhÃ´ng thá»ƒ gÃ¡n vai trÃ² BÃ¡c sÄ© táº¡i Ä‘Ã¢y. Vui lÃ²ng vÃ o 'Quáº£n LÃ½ BÃ¡c SÄ©' â†’ 'Tiáº¿p Nháº­n BÃ¡c SÄ© Má»›i'."
                        : "Vai trÃ² Ä‘Æ°á»£c chá»n khÃ´ng há»£p lá»‡.";
                return RedirectToAction(nameof(Dashboard));
            }

            var user = _context.Users.Find(userId);
            if (user == null) return NotFound();

            // Guard 1: KhÃ´ng cho phÃ©p thay Ä‘á»•i role cá»§a Admin báº¥t ká»³ (ká»ƒ cáº£ chÃ­nh mÃ¬nh)
            if (user.Role == "Admin")
            {
                TempData["ErrorMessage"] = $"KhÃ´ng thá»ƒ thay Ä‘á»•i vai trÃ² cá»§a Quáº£n Trá»‹ ViÃªn '{user.FullName}' qua giao diá»‡n nÃ y.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Guard 2: KhÃ´ng cho phÃ©p Ä‘á»•i role cá»§a Doctor qua ChangeRole
            if (user.Role == "Doctor")
            {
                TempData["ErrorMessage"] = $"KhÃ´ng thá»ƒ Ä‘á»•i vai trÃ² cá»§a BÃ¡c sÄ© '{user.FullName}' vÃ¬ tÃ i khoáº£n Ä‘ang liÃªn káº¿t há»“ sÆ¡ chuyÃªn mÃ´n vÃ  lá»‹ch khÃ¡m bá»‡nh.";
                return RedirectToAction(nameof(Dashboard));
            }

            user.Role = newRole;
            _context.SaveChanges();
            TempData["SuccessMessage"] = $"ÄÃ£ cáº­p nháº­t vai trÃ² cá»§a {user.FullName} thÃ nh: {newRole}";
            return RedirectToAction(nameof(Dashboard));
        }

        // ==========================================
        // Quáº£n lÃ½ BÃ¡c sÄ© & Tiáº¿p nháº­n nhÃ¢n sá»± y khoa
        // ==========================================
        public IActionResult Doctors()
        {
            var doctors = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .Include(d => d.Appointments)
                .OrderBy(d => d.Specialty != null ? d.Specialty.Name : "")
                .ThenBy(d => d.User != null ? d.User.FullName : "")
                .ToList();

            ViewBag.TotalDoctors = doctors.Count;
            ViewBag.Specialties = _context.Specialties.Where(s => s.IsActive).OrderBy(s => s.Name).ToList();
            return View(doctors);
        }

        [HttpGet]
        public IActionResult CreateDoctor()
        {
            ViewBag.Specialties = _context.Specialties.Where(s => s.IsActive).OrderBy(s => s.Name).ToList();
            return View(new CreateDoctorViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateDoctor(CreateDoctorViewModel model)
        {
            var emailLower = model.Email.Trim().ToLower();
            if (_context.Users.Any(u => u.Email.ToLower() == emailLower))
            {
                ModelState.AddModelError("Email", "Äá»‹a chá»‰ email nÃ y Ä‘Ã£ Ä‘Æ°á»£c sá»­ dá»¥ng trong há»‡ thá»‘ng.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Specialties = _context.Specialties.Where(s => s.IsActive).OrderBy(s => s.Name).ToList();
                return View(model);
            }

            var defaultAvatar = model.Gender == "Ná»¯"
                ? "https://images.unsplash.com/photo-1594824813576-96b618776269?w=400&auto=format&fit=crop&q=80"
                : "https://images.unsplash.com/photo-1622253692010-333f2da6031d?w=400&auto=format&fit=crop&q=80";

            var user = new User
            {
                FullName = model.FullName.Trim(),
                Email = emailLower,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                PhoneNumber = model.PhoneNumber?.Trim(),
                Gender = model.Gender,
                Address = model.Address?.Trim(),
                AvatarUrl = string.IsNullOrWhiteSpace(model.AvatarUrl) ? defaultAvatar : model.AvatarUrl.Trim(),
                Role = "Doctor",
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            var doctor = new Doctor
            {
                UserId = user.Id,
                SpecialtyId = model.SpecialtyId,
                Title = model.Title.Trim(),
                RoomNumber = model.RoomNumber.Trim(),
                ExperienceYears = model.ExperienceYears,
                ConsultationFee = model.ConsultationFee,
                Bio = model.Bio?.Trim() ?? string.Empty,
                Rating = 5.0,
                ReviewCount = 0
            };

            _context.Doctors.Add(doctor);
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"ÄÃ£ tiáº¿p nháº­n vÃ  táº¡o tÃ i khoáº£n BÃ¡c sÄ© {user.FullName} thÃ nh cÃ´ng!";
            return RedirectToAction(nameof(Doctors));
        }

        [HttpGet]
        public IActionResult EditDoctor(int id)
        {
            var doctor = _context.Doctors.Include(d => d.User).FirstOrDefault(d => d.Id == id);
            if (doctor == null) return NotFound();

            var model = new EditDoctorViewModel
            {
                Id = doctor.Id,
                UserId = doctor.UserId,
                FullName = doctor.User?.FullName ?? string.Empty,
                Email = doctor.User?.Email ?? string.Empty,
                PhoneNumber = doctor.User?.PhoneNumber,
                Gender = doctor.User?.Gender,
                Address = doctor.User?.Address,
                AvatarUrl = doctor.User?.AvatarUrl,
                SpecialtyId = doctor.SpecialtyId,
                Title = doctor.Title,
                RoomNumber = doctor.RoomNumber,
                ExperienceYears = doctor.ExperienceYears,
                ConsultationFee = doctor.ConsultationFee,
                Bio = doctor.Bio
            };

            ViewBag.Specialties = _context.Specialties.Where(s => s.IsActive).OrderBy(s => s.Name).ToList();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditDoctor(EditDoctorViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Specialties = _context.Specialties.Where(s => s.IsActive).OrderBy(s => s.Name).ToList();
                return View(model);
            }

            var doctor = _context.Doctors.Include(d => d.User).FirstOrDefault(d => d.Id == model.Id);
            if (doctor == null) return NotFound();

            if (doctor.User != null)
            {
                doctor.User.FullName = model.FullName.Trim();
                doctor.User.PhoneNumber = model.PhoneNumber?.Trim();
                doctor.User.Gender = model.Gender;
                doctor.User.Address = model.Address?.Trim();
                if (!string.IsNullOrWhiteSpace(model.AvatarUrl))
                {
                    doctor.User.AvatarUrl = model.AvatarUrl.Trim();
                }
            }

            doctor.SpecialtyId = model.SpecialtyId;
            doctor.Title = model.Title.Trim();
            doctor.RoomNumber = model.RoomNumber.Trim();
            doctor.ExperienceYears = model.ExperienceYears;
            doctor.ConsultationFee = model.ConsultationFee;
            doctor.Bio = model.Bio?.Trim() ?? string.Empty;

            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Cáº­p nháº­t há»“ sÆ¡ BÃ¡c sÄ© {doctor.User?.FullName} thÃ nh cÃ´ng!";
            return RedirectToAction(nameof(Doctors));
        }

        // US37 â€” Quáº£n lÃ½ dá»‹ch vá»¥ & biá»ƒu giÃ¡
        public IActionResult Services()
        {
            var services = _context.Services
                .Include(s => s.Specialty)
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Name)
                .ToList();
            return View(services);
        }

        public IActionResult CreateService()
        {
            ViewBag.Specialties = _context.Specialties.OrderBy(s => s.Name).ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateService(string name, string category, int? specialtyId,
            decimal price, int duration, string description, bool isActive = false)
        {
            var service = new Service
            {
                Name = name.Trim(),
                Category = category,
                SpecialtyId = specialtyId,
                Price = price,
                EstimatedDurationMinutes = duration,
                Description = description?.Trim() ?? string.Empty,
                IsActive = isActive
            };
            _context.Services.Add(service);
            _context.SaveChanges();
            TempData["SuccessMessage"] = $"ÄÃ£ thÃªm dá»‹ch vá»¥: {service.Name}";
            return RedirectToAction(nameof(Services));
        }

        public IActionResult EditService(int id)
        {
            var service = _context.Services.Find(id);
            if (service == null) return NotFound();
            ViewBag.Specialties = _context.Specialties.OrderBy(s => s.Name).ToList();
            return View(service);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditService(int id, string name, string category, int? specialtyId,
            decimal price, int duration, string description, bool isActive = false)
        {
            var service = _context.Services.Find(id);
            if (service == null) return NotFound();

            service.Name = name.Trim();
            service.Category = category;
            service.SpecialtyId = specialtyId;
            service.Price = price;
            service.EstimatedDurationMinutes = duration;
            service.Description = description?.Trim() ?? string.Empty;
            service.IsActive = isActive;

            _context.SaveChanges();
            TempData["SuccessMessage"] = $"ÄÃ£ cáº­p nháº­t dá»‹ch vá»¥: {service.Name}";
            return RedirectToAction(nameof(Services));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteService(int id)
        {
            var service = _context.Services.Find(id);
            if (service != null)
            {
                _context.Services.Remove(service);
                _context.SaveChanges();
                TempData["SuccessMessage"] = $"ÄÃ£ xÃ³a dá»‹ch vá»¥: {service.Name}";
            }
            return RedirectToAction(nameof(Services));
        }

        // ==========================================
        // US38: Quáº£n lÃ½ danh má»¥c trang thiáº¿t bá»‹ y táº¿ (Admin)
        // ==========================================
        public IActionResult Equipments(int? specialtyId, string? status, string? searchKeyword)
        {
            var query = _context.Equipments.Include(e => e.Specialty).AsQueryable();

            if (specialtyId.HasValue && specialtyId.Value > 0)
            {
                query = query.Where(e => e.SpecialtyId == specialtyId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(e => e.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                searchKeyword = searchKeyword.Trim().ToLower();
                query = query.Where(e => e.Name.ToLower().Contains(searchKeyword) || 
                                         e.Origin.ToLower().Contains(searchKeyword) || 
                                         e.Description.ToLower().Contains(searchKeyword));
            }

            var equipments = query.OrderBy(e => e.Name).ToList();

            ViewBag.Specialties = _context.Specialties.OrderBy(s => s.Name).ToList();
            ViewBag.SelectedSpecialty = specialtyId;
            ViewBag.SelectedStatus = status;
            ViewBag.SearchKeyword = searchKeyword;

            return View(equipments);
        }

        public IActionResult CreateEquipment()
        {
            ViewBag.Specialties = _context.Specialties.OrderBy(s => s.Name).ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateEquipment(string name, int? specialtyId, string origin, 
            string description, string imageUrl, string status)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("Name", "Vui lÃ²ng nháº­p tÃªn trang thiáº¿t bá»‹.");
                ViewBag.Specialties = _context.Specialties.OrderBy(s => s.Name).ToList();
                return View();
            }

            var equipment = new Equipment
            {
                Name = name.Trim(),
                SpecialtyId = specialtyId,
                Origin = string.IsNullOrWhiteSpace(origin) ? "Äá»©c" : origin.Trim(),
                Description = description?.Trim() ?? string.Empty,
                ImageUrl = imageUrl?.Trim() ?? "https://images.unsplash.com/photo-1516549655169-df83a0774514?w=600&auto=format&fit=crop&q=80",
                Status = string.IsNullOrWhiteSpace(status) ? "Äang váº­n hÃ nh tá»‘t" : status
            };

            _context.Equipments.Add(equipment);
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"ÄÃ£ thÃªm trang thiáº¿t bá»‹ y táº¿: {equipment.Name}";
            return RedirectToAction(nameof(Equipments));
        }

        public IActionResult EditEquipment(int id)
        {
            var equipment = _context.Equipments.Find(id);
            if (equipment == null) return NotFound();

            ViewBag.Specialties = _context.Specialties.OrderBy(s => s.Name).ToList();
            return View(equipment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditEquipment(int id, string name, int? specialtyId, string origin, 
            string description, string imageUrl, string status)
        {
            var equipment = _context.Equipments.Find(id);
            if (equipment == null) return NotFound();

            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("Name", "Vui lÃ²ng nháº­p tÃªn trang thiáº¿t bá»‹.");
                ViewBag.Specialties = _context.Specialties.OrderBy(s => s.Name).ToList();
                return View(equipment);
            }

            equipment.Name = name.Trim();
            equipment.SpecialtyId = specialtyId;
            equipment.Origin = string.IsNullOrWhiteSpace(origin) ? "Äá»©c" : origin.Trim();
            equipment.Description = description?.Trim() ?? string.Empty;
            equipment.ImageUrl = imageUrl?.Trim() ?? equipment.ImageUrl;
            equipment.Status = string.IsNullOrWhiteSpace(status) ? "Äang váº­n hÃ nh tá»‘t" : status;

            _context.SaveChanges();

            TempData["SuccessMessage"] = $"ÄÃ£ cáº­p nháº­t trang thiáº¿t bá»‹: {equipment.Name}";
            return RedirectToAction(nameof(Equipments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteEquipment(int id)
        {
            var equipment = _context.Equipments.Find(id);
            if (equipment != null)
            {
                _context.Equipments.Remove(equipment);
                _context.SaveChanges();
                TempData["SuccessMessage"] = $"ÄÃ£ xÃ³a trang thiáº¿t bá»‹: {equipment.Name}";
            }
            return RedirectToAction(nameof(Equipments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleEquipmentStatus(int id)
        {
            var equipment = _context.Equipments.Find(id);
            if (equipment != null)
            {
                equipment.Status = equipment.Status == "Äang váº­n hÃ nh tá»‘t" ? "Äang báº£o trÃ¬" : "Äang váº­n hÃ nh tá»‘t";
                _context.SaveChanges();
                TempData["SuccessMessage"] = $"ÄÃ£ chuyá»ƒn tráº¡ng thÃ¡i thiáº¿t bá»‹ {equipment.Name} sang: {equipment.Status}";
            }
            return RedirectToAction(nameof(Equipments));
        }

        // ==========================================
        // US-41: Nhật ký hoạt động hệ thống (Audit Log)
        // ==========================================
        public IActionResult AuditLog(string? search, string? action, string? role,
            string? dateFrom, string? dateTo, int page = 1)
        {
            var query = _context.AuditLogs.Include(a => a.User).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(a =>
                    a.ActorName.ToLower().Contains(term) ||
                    a.Description.ToLower().Contains(term) ||
                    a.EntityName.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(action))
            {
                query = query.Where(a => a.Action == action);
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(a => a.ActorRole == role);
            }

            if (!string.IsNullOrWhiteSpace(dateFrom) && DateTime.TryParse(dateFrom, out var from))
            {
                var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
                query = query.Where(a => a.CreatedAt >= fromUtc);
            }

            if (!string.IsNullOrWhiteSpace(dateTo) && DateTime.TryParse(dateTo, out var to))
            {
                var toUtc = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc);
                query = query.Where(a => a.CreatedAt < toUtc);
            }

            int pageSize = 20;
            int total = query.Count();
            int totalPages = (int)Math.Ceiling(total / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var logs = query.OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Search = search;
            ViewBag.SelectedAction = action;
            ViewBag.SelectedRole = role;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalLogs = total;

            return View(logs);
        }
    }
}