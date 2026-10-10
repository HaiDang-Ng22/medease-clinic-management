using System.Linq;
using Healthy_System.Data;
using Healthy_System.Models;
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
            var validRoles = new[] { "Admin", "Doctor", "Receptionist", "LabTech", "Accountant", "Patient" };
            if (!validRoles.Contains(newRole)) return BadRequest();

            var user = _context.Users.Find(userId);
            if (user != null)
            {
                user.Role = newRole;
                _context.SaveChanges();
                TempData["SuccessMessage"] = $"Đã cập nhật vai trò của {user.FullName} thành: {newRole}";
            }
            return RedirectToAction(nameof(Dashboard));
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
