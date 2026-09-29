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
    }
}
