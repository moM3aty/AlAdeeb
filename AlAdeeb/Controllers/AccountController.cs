using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using AlAdeeb.Data;
using AlAdeeb.Models;
using AlAdeeb.ViewModels;
using System;

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
                        if (user.Role == "Student")
                        {
                            // 1. توليد رمز تحقق من 4 أرقام
                            string code = new Random().Next(1000, 9999).ToString();
                            user.VerificationCode = code;
                            user.VerificationCodeExpiry = DateTime.Now.AddMinutes(10); // صلاحية الرمز 10 دقائق

                            _context.Update(user);
                            await _context.SaveChangesAsync();

                            // ملاحظة: هنا يمكنك ربط الـ API الخاص بإرسال الواتساب (WhatsApp)
                            // حالياً سنقوم بطباعته في النظام كرسالة تنبيهية للتجربة
                            TempData["DevCode"] = $"مرحباً، رمز التحقق الخاص بك هو: {code}";

                            // توجيه الطالب لصفحة إدخال الرمز
                            return RedirectToAction("VerifyCode", new { username = user.Username, rememberMe = model.RememberMe });
                        }
                        else
                        {
                            // دخول المدير مباشرة بدون رمز تحقق
                            return await CompleteSignIn(user, model.RememberMe);
                        }
                    }
                }
                ModelState.AddModelError(string.Empty, "بيانات الدخول غير صحيحة.");
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult VerifyCode(string username, bool rememberMe)
        {
            return View(new VerifyCodeViewModel { Username = username, RememberMe = rememberMe });
        }

        [HttpPost]
        public async Task<IActionResult> VerifyCode(VerifyCodeViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == model.Username);

                if (user != null && user.VerificationCode == model.Code && user.VerificationCodeExpiry > DateTime.Now)
                {
                    return await CompleteSignIn(user, model.RememberMe);
                }

                ModelState.AddModelError(string.Empty, "رمز التحقق غير صحيح أو منتهي الصلاحية.");
            }
            return View(model);
        }

        private async Task<IActionResult> CompleteSignIn(ApplicationUser user, bool rememberMe)
        {
            string newSessionId = Guid.NewGuid().ToString();
            user.CurrentSessionId = newSessionId;

            // تصفير رمز التحقق بعد الدخول الناجح
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

            if (user.Role == "Admin") return RedirectToAction("Index", "Admin");
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
                    CreatedAt = DateTime.Now
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
    }
}