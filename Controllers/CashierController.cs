using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Healthy_System.Data;
using Healthy_System.Models;
using Healthy_System.Models.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Healthy_System.Controllers
{
    [Authorize(Roles = "Accountant,Cashier,Admin,Receptionist")]
    public class CashierController : Controller
    {
        private readonly AppDbContext _context;

        public CashierController(AppDbContext context)
        {
            _context = context;
        }

        // --- Action 1: Danh sách bệnh nhân đã khám xong ---
        public async Task<IActionResult> Index(string search, string status)
        {
            // Chỉ lấy BN có Status == "Completed" (đã khám xong)
            var query = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Specialty)
                .Where(a => a.Status == "Completed")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(a =>
                    a.AppointmentCode.ToLower().Contains(term) ||
                    (a.Patient != null && a.Patient.FullName.ToLower().Contains(term)) ||
                    (a.Patient != null && a.Patient.PhoneNumber != null && a.Patient.PhoneNumber.Contains(term)));
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(a => a.PaymentStatus == status);
            }

            query = query.OrderByDescending(a => a.AppointmentDate);

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.UnpaidCount = await _context.Appointments
                .CountAsync(a => a.Status == "Completed" && a.PaymentStatus != "Paid");
            ViewBag.PaidCount = await _context.Appointments
                .CountAsync(a => a.Status == "Completed" && a.PaymentStatus == "Paid");

            var appointments = await query.Take(50).ToListAsync();

            // Load PatientCosts dictionary để hiển thị phí thực tế + tóm tắt XN/Thuốc
            var apptIds = appointments.Select(a => a.Id).ToList();
            var costs = await _context.PatientCosts
                .Where(c => apptIds.Contains(c.AppointmentId))
                .ToDictionaryAsync(c => c.AppointmentId);

            ViewBag.Costs = costs;

            return View(appointments);
        }

        // --- Action 2: Giao diện form xác nhận thanh toán ---
        [HttpGet]
        public async Task<IActionResult> ProcessPayment(int id)
        {
            var appt = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Specialty)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appt == null) return NotFound();

            var cost = await _context.PatientCosts.FirstOrDefaultAsync(c => c.AppointmentId == id);
            decimal total = cost != null ? cost.TotalAmount : (appt.Doctor?.ConsultationFee ?? 200000m);

            var vm = new PaymentViewModel
            {
                AppointmentId = appt.Id,
                AppointmentCode = appt.AppointmentCode,
                InvoiceCode = appt.InvoiceCode ?? $"HD-{DateTime.Now:yyyyMMdd}-{appt.Id:D4}",
                PatientName = appt.Patient?.FullName ?? "Chưa rõ",
                PatientPhone = appt.Patient?.PhoneNumber ?? "Chưa có SĐT",
                DoctorName = $"{appt.Doctor?.Title} {appt.Doctor?.User?.FullName}".Trim(),
                SpecialtyName = appt.Specialty?.Name ?? "",
                ConsultationFee = cost?.ConsultationFee ?? (appt.Doctor?.ConsultationFee ?? 200000m),
                TotalAmount = total,
                CashierName = User.Identity?.Name ?? "Thu ngân trực ca"
            };

            ViewBag.Cost = cost;

            return View(vm);
        }

        // --- Action 3: Xử lý lưu giao dịch thanh toán vào Database ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(PaymentViewModel model)
        {
            var appt = await _context.Appointments
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == model.AppointmentId);

            if (appt == null) return NotFound();

            var cost = await _context.PatientCosts.FirstOrDefaultAsync(c => c.AppointmentId == model.AppointmentId);
            decimal total = model.TotalAmount > 0 ? model.TotalAmount : (cost != null ? cost.TotalAmount : (appt.Doctor?.ConsultationFee ?? 200000m));

            appt.PaymentStatus = "Paid";
            appt.PaymentMethod = model.PaymentMethod;
            appt.PaidAmount = total;
            appt.PaidAt = DateTime.UtcNow;

            if (cost != null)
            {
                cost.PaymentStatus = "Paid";
                cost.PaymentMethod = model.PaymentMethod;
                cost.PaidAt = DateTime.UtcNow;
            }
            appt.CashierName = User.Identity?.Name ?? "Thu ngân";
            appt.InvoiceCode = string.IsNullOrEmpty(appt.InvoiceCode)
                ? $"INV-{DateTime.Now:yyyyMMdd}-{appt.Id:D4}"
                : appt.InvoiceCode;

            _context.Update(appt);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Thanh toán thành công hóa đơn {appt.InvoiceCode}!";
            return RedirectToAction("InvoiceReceipt", new { id = appt.Id });
        }

        // --- Action 4: In phiếu thu ---
        [HttpGet]
        public async Task<IActionResult> InvoiceReceipt(int id)
        {
            var appt = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Specialty)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appt == null || appt.PaymentStatus != "Paid") return RedirectToAction("Index");

            var cost = await _context.PatientCosts.FirstOrDefaultAsync(c => c.AppointmentId == id);
            ViewBag.Cost = cost;

            return View(appt);
        }
    }
}
