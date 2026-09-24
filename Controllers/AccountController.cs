using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BCrypt.Net;
using Healthy_System.Data;
using Healthy_System.Models;
using Healthy_System.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Healthy_System.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        // US-08: Register Account
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var emailLower = model.Email.Trim().ToLower();
            if (_context.Users.Any(u => u.Email.ToLower() == emailLower))
            {
                ModelState.AddModelError("Email", "Địa chỉ email này đã được sử dụng trong hệ thống.");
                return View(model);
            }

            var newUser = new User
            {
                FullName = model.FullName.Trim(),
                Email = emailLower,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                PhoneNumber = model.PhoneNumber?.Trim(),
                DateOfBirth = model.DateOfBirth,
                Gender = model.Gender,
                Address = model.Address?.Trim(),
                Role = "Patient",
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            _context.SaveChanges();

            // Add Welcome Notification
            _context.Notifications.Add(new Notification
            {
                UserId = newUser.Id,
                Title = "Chào mừng bạn đến với Healthy System",
                Message = $"Xin chào {newUser.FullName}! Bạn đã đăng ký tài khoản thành công. Bây giờ bạn có thể đặt lịch khám bệnh trực tuyến mọi lúc mọi nơi.",
                Type = "General",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            _context.SaveChanges();

            // Auto Login
            await SignInUser(newUser, isPersistent: true);

            TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Chào mừng bạn đến với Healthy System.";
            return RedirectToAction("Index", "Home");
        }

        // US-09: Login
        [HttpGet]
        public IActionResult Login(string? returnUrl)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var emailLower = model.Email.Trim().ToLower();
            var user = _context.Users.FirstOrDefault(u => u.Email.ToLower() == emailLower);

            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
                return View(model);
            }

            await SignInUser(user, model.RememberMe);

            TempData["SuccessMessage"] = $"Đăng nhập thành công! Chào mừng {user.FullName}.";

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            // Redirect to appropriate portal based on role
            return user.Role switch
            {
                "Doctor" => RedirectToAction("Dashboard", "Doctor"),
                "Receptionist" => RedirectToAction("Dashboard", "Receptionist"),
                "Admin" => RedirectToAction("Dashboard", "Admin"),
                _ => RedirectToAction("Index", "Home")
            };
        }

        // US-21: Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["SuccessMessage"] = "Bạn đã đăng xuất khỏi hệ thống thành công.";
            return RedirectToAction("Index", "Home");
        }

        // US-20: Update Personal Profile
        [Authorize]
        [HttpGet]
        public IActionResult Profile()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
            {
                return RedirectToAction("Login");
            }

            var user = _context.Users.Find(userId);
            if (user == null)
            {
                return NotFound();
            }

            var model = new ProfileViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                DateOfBirth = user.DateOfBirth,
                Gender = user.Gender,
                Address = user.Address,
                AvatarUrl = user.AvatarUrl,
                Role = user.Role
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Profile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId) || userId != model.Id)
            {
                return Unauthorized();
            }

            var user = _context.Users.Find(userId);
            if (user == null)
            {
                return NotFound();
            }

            user.FullName = model.FullName.Trim();
            user.PhoneNumber = model.PhoneNumber?.Trim();
            user.DateOfBirth = model.DateOfBirth;
            user.Gender = model.Gender;
            user.Address = model.Address?.Trim();
            user.AvatarUrl = model.AvatarUrl?.Trim();

            _context.SaveChanges();

            TempData["SuccessMessage"] = "Cập nhật hồ sơ cá nhân thành công!";
            return RedirectToAction(nameof(Profile));
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        private async Task SignInUser(User user, bool isPersistent)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = isPersistent,
                ExpiresUtc = DateTime.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
        }
    }
}
