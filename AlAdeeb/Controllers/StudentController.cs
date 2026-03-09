using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;
using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using System.IO;
using AlAdeeb.Data;
using AlAdeeb.Models;

namespace AlAdeeb.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<ApplicationUser> _passwordHasher;

        public StudentController(AppDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<ApplicationUser>();
        }

        private int GetCurrentStudentId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        }

        // ==========================================
        // اللوحة الرئيسية للطالب
        // ==========================================
        public async Task<IActionResult> Dashboard()
        {
            int studentId = GetCurrentStudentId();
            var requests = await _context.SubscriptionRequests
                .Include(r => r.Course)
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            ViewBag.StudentName = User.Identity.Name;
            return View(requests);
        }

        // ==========================================
        // استكشاف الدورات (متاحة للزوار والطلاب)
        // ==========================================
        [AllowAnonymous]
        public async Task<IActionResult> BrowseCourses()
        {
            var courses = await _context.Courses.Where(c => c.IsActive).OrderByDescending(c => c.Id).ToListAsync();

            if (User.Identity.IsAuthenticated && User.IsInRole("Student"))
            {
                int studentId = GetCurrentStudentId();
                ViewBag.MySubscriptions = await _context.SubscriptionRequests
                    .Where(r => r.StudentId == studentId)
                    .Select(r => r.CourseId)
                    .ToListAsync();
            }
            else
            {
                ViewBag.MySubscriptions = new List<int>();
            }

            return View(courses);
        }

        // ==========================================
        // قاعة المذاكرة (محتوى الدورة) وإجبار الترتيب
        // ==========================================
        public async Task<IActionResult> CourseContent(int id)
        {
            int studentId = GetCurrentStudentId();

            var subscription = await _context.SubscriptionRequests
                .FirstOrDefaultAsync(r => r.StudentId == studentId && r.CourseId == id && r.Status == "Approved");

            if (subscription == null) return Unauthorized("غير مصرح لك بدخول هذه الدورة أو أن اشتراكك غير مفعل.");

            ViewBag.IsExpired = subscription.ExpiryDate.HasValue && subscription.ExpiryDate.Value < DateTime.Now;

            var course = await _context.Courses
                .Include(c => c.Lessons)
                    .ThenInclude(l => l.Materials)
                .Include(c => c.Lessons)
                    .ThenInclude(l => l.Quizzes)
                .Include(c => c.Quizzes)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return NotFound();

            ViewBag.LiveSessions = await _context.Set<LiveSession>()
                .Where(l => l.CourseId == id)
                .OrderByDescending(l => l.ScheduledDate)
                .ToListAsync();

            // خوارزمية إجبار الترتيب (Sequential Learning)
            var passedScores = await _context.StudentScores
                .Where(s => s.StudentId == studentId && s.Passed)
                .Select(s => s.QuizId)
                .ToListAsync();

            var unlockedLessonIds = new List<int>();
            bool previousLessonPassed = true; // الدرس الأول مفتوح دائماً

            foreach (var lesson in course.Lessons.OrderBy(l => l.OrderIndex))
            {
                if (previousLessonPassed)
                {
                    unlockedLessonIds.Add(lesson.Id);
                }

                // للتمكن من فتح الدرس التالي، يجب اجتياز جميع اختبارات الدرس الحالي
                if (lesson.Quizzes != null && lesson.Quizzes.Any())
                {
                    previousLessonPassed = lesson.Quizzes.All(q => passedScores.Contains(q.Id));
                }
            }

            ViewBag.UnlockedLessonIds = unlockedLessonIds;
            return View(course);
        }

        // ==========================================
        // محاكي الاختبارات العشوائي
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> TakeQuiz(int id)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quiz == null) return NotFound();

            if (quiz.IsSimulator)
            {
                int courseId = quiz.CourseId ?? (quiz.Lesson != null ? quiz.Lesson.CourseId : 0);

                // سحب الأقسام العشوائية من بنك الأسئلة بناءً على العدد المحدد في إعدادات الاختبار
                var randomSections = await _context.QuestionBankSections
                    .Include(s => s.Questions)
                    .Where(s => s.CourseId == courseId)
                    .OrderBy(x => Guid.NewGuid())
                    .Take(quiz.SimulatorSectionsCount)
                    .ToListAsync();

                var simulatedQuestions = new List<Question>();
                foreach (var sec in randomSections)
                {
                    foreach (var bq in sec.Questions)
                    {
                        simulatedQuestions.Add(new Question
                        {
                            Id = bq.Id,
                            SkillType = bq.SkillType,
                            QuestionText = bq.QuestionText,
                            QuestionImagePath = bq.QuestionImagePath,
                            OptionA = bq.OptionA,
                            OptionB = bq.OptionB,
                            OptionC = bq.OptionC,
                            OptionD = bq.OptionD,
                            CorrectOption = bq.CorrectOption
                        });
                    }
                }

                // خلط جميع الأسئلة المستخرجة من الأقسام ليصبح الاختبار عشوائياً بالكامل
                quiz.Questions = simulatedQuestions.OrderBy(x => Guid.NewGuid()).ToList();
            }

            return View(quiz);
        }

        // ==========================================
        // سجل النتائج والشهادات
        // ==========================================
        public async Task<IActionResult> Results()
        {
            int studentId = GetCurrentStudentId();
            var scores = await _context.StudentScores
                .Include(s => s.Quiz)
                .ThenInclude(q => q.Course)
                .Where(s => s.StudentId == studentId)
                .OrderByDescending(s => s.DateTaken)
                .ToListAsync();
            return View(scores);
        }

        // ==========================================
        // الاشتراك ورفع الإيصال
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Subscribe(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();
            return View(course);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitSubscription(int courseId, IFormFile receiptImage)
        {
            int studentId = GetCurrentStudentId();

            // منع تكرار الطلب لنفس الكورس
            var existing = await _context.SubscriptionRequests.FirstOrDefaultAsync(r => r.CourseId == courseId && r.StudentId == studentId);
            if (existing != null)
            {
                TempData["ErrorMessage"] = "لديك طلب اشتراك مسبق في هذه الدورة.";
                return RedirectToAction(nameof(Dashboard));
            }

            var request = new SubscriptionRequest
            {
                StudentId = studentId,
                CourseId = courseId,
                Status = "Pending",
                RequestDate = DateTime.Now
            };

            if (receiptImage != null && receiptImage.Length > 0)
            {
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/receipts");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + receiptImage.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await receiptImage.CopyToAsync(fileStream);
                }
                request.ReceiptImagePath = "/uploads/receipts/" + uniqueFileName;
            }

            _context.SubscriptionRequests.Add(request);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم رفع طلب الاشتراك بنجاح، يرجى انتظار التفعيل من الإدارة.";
            return RedirectToAction(nameof(Dashboard));
        }

        // ==========================================
        // الملف الشخصي للطالب (تحديث البيانات)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var studentId = GetCurrentStudentId();
            var user = await _context.Users.FindAsync(studentId);
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string FullName, string PhoneNumber, string NewPassword)
        {
            var studentId = GetCurrentStudentId();
            var user = await _context.Users.FindAsync(studentId);

            if (user != null)
            {
                user.FullName = FullName;
                user.PhoneNumber = PhoneNumber;
                user.Username = PhoneNumber;

                if (!string.IsNullOrWhiteSpace(NewPassword))
                {
                    user.PasswordHash = _passwordHasher.HashPassword(user, NewPassword);
                }

                _context.Update(user);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تحديث بياناتك بنجاح!";
            }

            return RedirectToAction(nameof(Profile));
        }
    }
}