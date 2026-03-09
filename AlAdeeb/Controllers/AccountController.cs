using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using AlAdeeb.Data;
using AlAdeeb.Models;
using AlAdeeb.ViewModels;
using System;
using System.Linq;

namespace AlAdeeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<ApplicationUser> _passwordHasher;

        public AccountController(AppDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<ApplicationUser>();
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated) return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == model.Username || u.PhoneNumber == model.Username);

                if (user != null)
                {
                    var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

                    if (result == PasswordVerificationResult.Success)
                    {
                        return await CompleteSignIn(user, model.RememberMe);
                    }
                }
                ModelState.AddModelError(string.Empty, "بيانات الدخول غير صحيحة.");
            }
            return View(model);
        }

        private async Task<IActionResult> CompleteSignIn(ApplicationUser user, bool rememberMe)
        {
            string newSessionId = Guid.NewGuid().ToString();

            var activeSessions = string.IsNullOrEmpty(user.ActiveSessionsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(user.ActiveSessionsJson);

            activeSessions.Add(newSessionId);

            int maxDevices = user.AllowedDevicesCount > 0 ? user.AllowedDevicesCount : 1;
            if (activeSessions.Count > maxDevices)
            {
                activeSessions = activeSessions.Skip(activeSessions.Count - maxDevices).ToList();
            }

            user.ActiveSessionsJson = JsonSerializer.Serialize(activeSessions);
            user.CurrentSessionId = newSessionId;

            user.VerificationCode = null;
            user.VerificationCodeExpiry = null;

            _context.Update(user);
            await _context.SaveChangesAsync();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("SessionId", newSessionId)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties { IsPersistent = rememberMe });

            if (user.Role == "Admin" || user.Role == "Teacher") return RedirectToAction("Index", "Admin");
            else return RedirectToAction("Dashboard", "Student");
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (await _context.Users.AnyAsync(u => u.PhoneNumber == model.PhoneNumber))
                {
                    ModelState.AddModelError("PhoneNumber", "رقم الجوال مسجل مسبقاً.");
                    return View(model);
                }

                var newUser = new ApplicationUser
                {
                    FullName = model.FullName,
                    Username = model.PhoneNumber,
                    PhoneNumber = model.PhoneNumber,
                    Role = "Student",
                    CreatedAt = DateTime.Now,
                    AllowedDevicesCount = 1
                };

                newUser.PasswordHash = _passwordHasher.HashPassword(newUser, model.Password);

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                return await Login(new LoginViewModel { Username = model.PhoneNumber, Password = model.Password });
            }
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // ========================================================
        // نظام استعادة وتغيير كلمة المرور المضاف حديثاً
        // ========================================================
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                ModelState.AddModelError("", "يرجى إدخال رقم الجوال.");
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
            if (user != null)
            {
                // توليد كود مؤقت (يمكن لاحقاً ربطه بخدمة SMS أو الواتساب)
                string code = new Random().Next(1000, 9999).ToString();
                user.VerificationCode = code;
                user.VerificationCodeExpiry = DateTime.Now.AddMinutes(15);
                await _context.SaveChangesAsync();

                TempData["DevCode"] = $"رمز استعادة كلمة المرور الخاص بك هو: {code}";
                return RedirectToAction("ResetPassword", new { phone = phoneNumber });
            }

            ModelState.AddModelError("", "رقم الجوال غير مسجل لدينا في المنصة.");
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return RedirectToAction("Login");
            ViewBag.Phone = phone;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string phoneNumber, string code, string newPassword)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);

            if (user != null && user.VerificationCode == code && user.VerificationCodeExpiry > DateTime.Now)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
                user.VerificationCode = null;
                user.VerificationCodeExpiry = null;

                // طرد الطالب من جميع الأجهزة الأخرى عند تغيير كلمة المرور كإجراء أمني
                user.ActiveSessionsJson = "[]";

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "تم تغيير كلمة المرور بنجاح، يمكنك تسجيل الدخول الآن بكلمة المرور الجديدة.";
                return RedirectToAction("Login");
            }

            ModelState.AddModelError("", "رمز التحقق غير صحيح أو منتهي الصلاحية.");
            ViewBag.Phone = phoneNumber;
            return View();
        }
    }
}