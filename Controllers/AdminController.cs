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

        // US35 — Quản lý tài khoản & phân quyền
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
            // RBAC Rule: Chỉ cho phép gán các vai trò nhân sự thông thường qua UI.
            // - "Admin" : KHÔNG cho phép gán qua UI — Admin chỉ tạo qua DB seed ban đầu.
            // - "Doctor": KHÔNG cho phép gán qua UI — phải qua flow "Tiếp Nhận Bác Sĩ".
            var allowedRoles = new[] { "Receptionist", "LabTech", "Accountant", "Patient" };
            if (!allowedRoles.Contains(newRole))
            {
                TempData["ErrorMessage"] = newRole == "Admin"
                    ? "Không thể cấp quyền Quản Trị Viên qua giao diện. Liên hệ quản trị hệ thống nếu cần tạo Admin mới."
                    : newRole == "Doctor"
                        ? "Không thể gán vai trò Bác sĩ tại đây. Vui lòng vào 'Quản Lý Bác Sĩ' → 'Tiếp Nhận Bác Sĩ Mới'."
                        : "Vai trò được chọn không hợp lệ.";
                return RedirectToAction(nameof(Dashboard));
            }

            var user = _context.Users.Find(userId);
            if (user == null) return NotFound();

            // Guard 1: Không cho phép thay đổi role của Admin bất kỳ (kể cả chính mình)
            if (user.Role == "Admin")
            {
                TempData["ErrorMessage"] = $"Không thể thay đổi vai trò của Quản Trị Viên '{user.FullName}' qua giao diện này.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Guard 2: Không cho phép đổi role của Doctor qua ChangeRole
            if (user.Role == "Doctor")
            {
                TempData["ErrorMessage"] = $"Không thể đổi vai trò của Bác sĩ '{user.FullName}' vì tài khoản đang liên kết hồ sơ chuyên môn và lịch khám bệnh.";
                return RedirectToAction(nameof(Dashboard));
            }

            user.Role = newRole;
            _context.SaveChanges();
            TempData["SuccessMessage"] = $"Đã cập nhật vai trò của {user.FullName} thành: {newRole}";
            return RedirectToAction(nameof(Dashboard));
        }

        // ==========================================
        // Quản lý Bác sĩ & Tiếp nhận nhân sự y khoa
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
                ModelState.AddModelError("Email", "Địa chỉ email này đã được sử dụng trong hệ thống.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Specialties = _context.Specialties.Where(s => s.IsActive).OrderBy(s => s.Name).ToList();
                return View(model);
            }

            var defaultAvatar = model.Gender == "Nữ"
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

            TempData["SuccessMessage"] = $"Đã tiếp nhận và tạo tài khoản Bác sĩ {user.FullName} thành công!";
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

            TempData["SuccessMessage"] = $"Cập nhật hồ sơ Bác sĩ {doctor.User?.FullName} thành công!";
            return RedirectToAction(nameof(Doctors));
        }

        // US37 — Quản lý dịch vụ & biểu giá
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
            TempData["SuccessMessage"] = $"Đã thêm dịch vụ: {service.Name}";
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
            TempData["SuccessMessage"] = $"Đã cập nhật dịch vụ: {service.Name}";
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
                TempData["SuccessMessage"] = $"Đã xóa dịch vụ: {service.Name}";
            }
            return RedirectToAction(nameof(Services));
        }

        // ==========================================
        // US38: Quản lý danh mục trang thiết bị y tế (Admin)
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
                ModelState.AddModelError("Name", "Vui lòng nhập tên trang thiết bị.");
                ViewBag.Specialties = _context.Specialties.OrderBy(s => s.Name).ToList();
                return View();
            }

            var equipment = new Equipment
            {
                Name = name.Trim(),
                SpecialtyId = specialtyId,
                Origin = string.IsNullOrWhiteSpace(origin) ? "Đức" : origin.Trim(),
                Description = description?.Trim() ?? string.Empty,
                ImageUrl = imageUrl?.Trim() ?? "https://images.unsplash.com/photo-1516549655169-df83a0774514?w=600&auto=format&fit=crop&q=80",
                Status = string.IsNullOrWhiteSpace(status) ? "Đang vận hành tốt" : status
            };

            _context.Equipments.Add(equipment);
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã thêm trang thiết bị y tế: {equipment.Name}";
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
                ModelState.AddModelError("Name", "Vui lòng nhập tên trang thiết bị.");
                ViewBag.Specialties = _context.Specialties.OrderBy(s => s.Name).ToList();
                return View(equipment);
            }

            equipment.Name = name.Trim();
            equipment.SpecialtyId = specialtyId;
            equipment.Origin = string.IsNullOrWhiteSpace(origin) ? "Đức" : origin.Trim();
            equipment.Description = description?.Trim() ?? string.Empty;
            equipment.ImageUrl = imageUrl?.Trim() ?? equipment.ImageUrl;
            equipment.Status = string.IsNullOrWhiteSpace(status) ? "Đang vận hành tốt" : status;

            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã cập nhật trang thiết bị: {equipment.Name}";
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
                TempData["SuccessMessage"] = $"Đã xóa trang thiết bị: {equipment.Name}";
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
                equipment.Status = equipment.Status == "Đang vận hành tốt" ? "Đang bảo trì" : "Đang vận hành tốt";
                _context.SaveChanges();
                TempData["SuccessMessage"] = $"Đã chuyển trạng thái thiết bị {equipment.Name} sang: {equipment.Status}";
            }
            return RedirectToAction(nameof(Equipments));
        }
    }
}
