using AlAdeeb.Data;
using AlAdeeb.Models;
using AlAdeeb.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace AlAdeeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<ApplicationUser> _passwordHasher;
        private readonly IWebHostEnvironment _webHostEnvironment;

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
        [HttpGet]
        public async Task<IActionResult> QuestionBank(int courseId)
        {
            var course = await _context.Courses
                .Include(c => c.QuestionBankSections)
                    .ThenInclude(s => s.Questions)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null) return NotFound();
            return View(course);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBankSection(int courseId, string title)
        {
            if (!string.IsNullOrEmpty(title))
            {
                var section = new QuestionBankSection { CourseId = courseId, Title = title };
                _context.QuestionBankSections.Add(section);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إضافة قسم جديد لبنك الأسئلة.";
            }
            return RedirectToAction("QuestionBank", new { courseId = courseId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBankQuestion(int sectionId, int courseId, string SkillType, string QuestionText, string OptionA, string OptionB, string OptionC, string OptionD, string CorrectOption, IFormFile ImageFile)
        {
            var q = new BankQuestion
            {
                QuestionBankSectionId = sectionId,
                SkillType = SkillType ?? "أسئلة منوعة",
                QuestionText = QuestionText ?? "",
                OptionA = OptionA ?? "",
                OptionB = OptionB ?? "",
                OptionC = OptionC ?? "",
                OptionD = OptionD ?? "",
                CorrectOption = CorrectOption ?? "A"
            };

            if (ImageFile != null && ImageFile.Length > 0)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads/questions");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(ImageFile.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create)) { await ImageFile.CopyToAsync(fileStream); }
                q.QuestionImagePath = "/uploads/questions/" + uniqueFileName;
            }
            else { q.QuestionImagePath = ""; }

            if (string.IsNullOrEmpty(q.QuestionText) && !string.IsNullOrEmpty(q.QuestionImagePath)) { q.QuestionText = "[سؤال مصور]"; }

            _context.BankQuestions.Add(q);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حفظ السؤال في البنك.";
            return RedirectToAction("QuestionBank", new { courseId = courseId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBankQuestion(int id, int courseId)
        {
            var q = await _context.BankQuestions.FindAsync(id);
            if (q != null) { _context.BankQuestions.Remove(q); await _context.SaveChangesAsync(); }
            return RedirectToAction("QuestionBank", new { courseId = courseId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBankSection(int id, int courseId)
        {
            var s = await _context.QuestionBankSections.FindAsync(id);
            if (s != null) { _context.QuestionBankSections.Remove(s); await _context.SaveChangesAsync(); }
            return RedirectToAction("QuestionBank", new { courseId = courseId });
        }

        // ==========================================
        // إدارة الاختبارات والمحاكي
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> AddQuiz(int courseId, int? lessonId, bool isFinalExam = false)
        {
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound();
            ViewBag.LessonId = lessonId;
            ViewBag.IsFinalExam = isFinalExam;
            return View(course);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveQuiz(int CourseId, int? LessonId, string QuizTitle, int DurationInMinutes, bool IsFinalExam, bool IsSimulator, int SimulatorSectionsCount, List<Question> Questions)
        {
            if (Questions == null) Questions = new List<Question>();
            var newQuiz = new Quiz
            {
                CourseId = IsFinalExam ? CourseId : null,
                LessonId = IsFinalExam ? null : LessonId,
                Title = string.IsNullOrEmpty(QuizTitle) ? "اختبار" : QuizTitle,
                DurationInMinutes = DurationInMinutes,
                IsFinalExam = IsFinalExam,
                IsSimulator = IsSimulator,
                SimulatorSectionsCount = IsSimulator ? SimulatorSectionsCount : 0,
                Questions = new List<Question>()
            };

            if (!IsSimulator)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads/questions");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                for (int i = 0; i < Questions.Count; i++)
                {
                    var q = Questions[i];
                    if (q == null) continue;
                    var fileKey = $"Questions[{i}].ImageFile";
                    var imageFile = Request.Form.Files[fileKey];
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(imageFile.FileName);
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        using (var fileStream = new FileStream(filePath, FileMode.Create)) { await imageFile.CopyToAsync(fileStream); }
                        q.QuestionImagePath = "/uploads/questions/" + uniqueFileName;
                    }
                    q.QuestionText = q.QuestionText ?? ""; q.OptionA = q.OptionA ?? ""; q.OptionB = q.OptionB ?? ""; q.OptionC = q.OptionC ?? ""; q.OptionD = q.OptionD ?? ""; q.CorrectOption = q.CorrectOption ?? "A"; q.SkillType = q.SkillType ?? "أسئلة منوعة";
                    newQuiz.Questions.Add(q);
                }
            }

            _context.Quizzes.Add(newQuiz);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حفظ الاختبار بنجاح!";
            return RedirectToAction("Details", "Course", new { id = CourseId });
        }

        // ==========================================
        // إدارة إعدادات المنصة (الشاشة الرئيسية)
        // ==========================================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PlatformSettings()
        {
            var settings = await _context.PlatformSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new PlatformSetting { PromoVideoUrl = "", PlacementTestQuizId = null };
                _context.PlatformSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            // جلب جميع الاختبارات ليتمكن المدير من اختيار اختبار تحديد المستوى
            ViewBag.AllQuizzes = await _context.Quizzes.Include(q => q.Course).ToListAsync();

            return View(settings);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePlatformSettings(PlatformSetting model)
        {
            var settings = await _context.PlatformSettings.FirstOrDefaultAsync();
            if (settings != null)
            {
                settings.PromoVideoUrl = model.PromoVideoUrl;
                settings.PlacementTestQuizId = model.PlacementTestQuizId;
                _context.Update(settings);
            }
            else
            {
                _context.PlatformSettings.Add(model);
            }
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث إعدادات الشاشة الرئيسية بنجاح.";
            return RedirectToAction(nameof(PlatformSettings));
        }
    }
}
    
