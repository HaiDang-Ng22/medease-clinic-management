using System;
using System.Diagnostics;
using System.Linq;
using Healthy_System.Data;
using Healthy_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Healthy_System.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        // US-01: View Homepage
        public IActionResult Index()
        {
            // If logged in as Doctor, Receptionist or Admin -> redirect to dedicated portal
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Doctor"))
                {
                    return RedirectToAction("Dashboard", "Doctor");
                }
                if (User.IsInRole("Receptionist"))
                {
                    return RedirectToAction("Dashboard", "Receptionist");
                }
                if (User.IsInRole("Admin"))
                {
                    return RedirectToAction("Dashboard", "Admin");
                }
            }

            ViewBag.Specialties = _context.Specialties.Where(s => s.IsActive).Take(6).ToList();
            ViewBag.Doctors = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .OrderByDescending(d => d.Rating)
                .Take(4)
                .ToList();
            ViewBag.Services = _context.Services.Where(s => s.IsActive).Take(6).ToList();
            ViewBag.Equipments = _context.Equipments.Take(3).ToList();

            // Stats
            ViewBag.DoctorCount = _context.Doctors.Count();
            ViewBag.SpecialtyCount = _context.Specialties.Count();
            ViewBag.PatientCount = _context.Users.Count(u => u.Role == "Patient") + 1500;
            ViewBag.SatisfactionRate = 98;

            return View();
        }

        // US-02: View Specialty List
        public IActionResult Specialties()
        {
            var list = _context.Specialties
                .Include(s => s.Doctors)
                .Include(s => s.Services)
                .Where(s => s.IsActive)
                .ToList();
            return View(list);
        }

        public IActionResult SpecialtyDetail(int id)
        {
            var specialty = _context.Specialties
                .Include(s => s.Doctors).ThenInclude(d => d.User)
                .Include(s => s.Services)
                .FirstOrDefault(s => s.Id == id);

            if (specialty == null)
            {
                return NotFound();
            }

            return View(specialty);
        }

        // US-03: View Doctor List
        public IActionResult Doctors(int? specialtyId, string? search)
        {
            var query = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .AsQueryable();

            if (specialtyId.HasValue && specialtyId.Value > 0)
            {
                query = query.Where(d => d.SpecialtyId == specialtyId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(d => d.User != null && d.User.FullName.ToLower().Contains(term)
                                      || d.Specialty != null && d.Specialty.Name.ToLower().Contains(term));
            }

            ViewBag.Specialties = _context.Specialties.Where(s => s.IsActive).ToList();
            ViewBag.SelectedSpecialtyId = specialtyId;
            ViewBag.SearchTerm = search;

            var doctors = query.OrderByDescending(d => d.Rating).ToList();
            return View(doctors);
        }

        // US-04: View Doctor Detail
        public IActionResult DoctorDetail(int id)
        {
            var doctor = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .Include(d => d.Schedules.Where(s => s.WorkDate >= DateTime.Today && s.IsAvailable))
                .FirstOrDefault(d => d.Id == id);

            if (doctor == null)
            {
                return NotFound();
            }

            return View(doctor);
        }

        // US-05: View Services & Pricing
        public IActionResult Services(string? category)
        {
            var query = _context.Services
                .Include(s => s.Specialty)
                .Where(s => s.IsActive);

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(s => s.Category == category);
            }

            ViewBag.SelectedCategory = category;
            ViewBag.Categories = _context.Services.Select(s => s.Category).Distinct().ToList();

            var services = query.OrderBy(s => s.Category).ThenBy(s => s.Price).ToList();
            return View(services);
        }

        // US-06: View Equipment Info
        public IActionResult Equipment()
        {
            var equipments = _context.Equipments
                .Include(e => e.Specialty)
                .ToList();
            return View(equipments);
        }

        // US-07: Contact & Hotline
        public IActionResult Contact()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SubmitContact(ContactMessage model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.UtcNow;
                _context.ContactMessages.Add(model);
                _context.SaveChanges();

                TempData["SuccessMessage"] = "Cảm ơn bạn đã liên hệ! Ban tư vấn của Healthy System sẽ phản hồi cho bạn trong thời gian sớm nhất.";
                return RedirectToAction(nameof(Contact));
            }

            return View("Contact", model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
