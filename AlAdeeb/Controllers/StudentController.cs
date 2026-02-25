using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System;
using AlAdeeb.Data;
using AlAdeeb.Models;

namespace AlAdeeb.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly PasswordHasher<ApplicationUser> _passwordHasher;

        public StudentController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _passwordHasher = new PasswordHasher<ApplicationUser>();
        }

        private int GetCurrentStudentId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        }

        public async Task<IActionResult> Dashboard()
        {
            int studentId = GetCurrentStudentId();

            var approvedSubscriptions = await _context.SubscriptionRequests
                .Include(r => r.Course)
                .Where(r => r.StudentId == studentId && r.Status == "Approved")
                .ToListAsync();

            ViewBag.StudentName = User.FindFirstValue(ClaimTypes.Name);
            return View(approvedSubscriptions);
        }

        public async Task<IActionResult> BrowseCourses()
        {
            int studentId = GetCurrentStudentId();

            var subscribedCourseIds = await _context.SubscriptionRequests
                .Where(r => r.StudentId == studentId)
                .Select(r => r.CourseId)
                .ToListAsync();

            var availableCourses = await _context.Courses
                .Where(c => c.IsActive && !subscribedCourseIds.Contains(c.Id))
                .ToListAsync();

            return View(availableCourses);
        }

        [HttpGet]
        public async Task<IActionResult> Subscribe(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null || !course.IsActive) return NotFound();

            return View(course);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitSubscription(int courseId, IFormFile receiptImage)
        {
            int studentId = GetCurrentStudentId();

            var existingRequest = await _context.SubscriptionRequests
                .FirstOrDefaultAsync(r => r.StudentId == studentId && r.CourseId == courseId);

            if (existingRequest != null)
            {
                TempData["ErrorMessage"] = "لديك طلب اشتراك مسبق في هذا الكورس.";
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
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads/receipts");
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

            TempData["SuccessMessage"] = "تم إرسال طلبك بنجاح! سيتم تفعيل حسابك بمجرد مراجعة الإيصال.";
            return RedirectToAction(nameof(Dashboard));
        }

        // تم إصلاح الخطأ هنا (استخدام Lessons بدلاً من Materials مباشرة)
        public async Task<IActionResult> CourseContent(int id)
        {
            int studentId = GetCurrentStudentId();

            var subscription = await _context.SubscriptionRequests
                .FirstOrDefaultAsync(r => r.StudentId == studentId && r.CourseId == id && r.Status == "Approved");

            if (subscription == null) return Unauthorized("غير مصرح لك بدخول هذا الكورس.");

            // تمرير حالة الاشتراك وهل هو منتهي أم لا للفيو
            ViewBag.IsExpired = subscription.ExpiryDate.HasValue && subscription.ExpiryDate.Value < DateTime.Now;

            var course = await _context.Courses
                .Include(c => c.Lessons)
                    .ThenInclude(l => l.Materials)
                .Include(c => c.Lessons)
                    .ThenInclude(l => l.Quizzes)
                .Include(c => c.Quizzes)
                .FirstOrDefaultAsync(c => c.Id == id);

            ViewBag.LiveSessions = await _context.Set<LiveSession>()
                .Where(l => l.CourseId == id)
                .OrderByDescending(l => l.ScheduledDate)
                .ToListAsync();

            return View(course);
        }

        [HttpGet]
        public async Task<IActionResult> TakeQuiz(int id)
        {
            int studentId = GetCurrentStudentId();

            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                .Include(q => q.Lesson)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quiz == null) return NotFound("الاختبار غير موجود");

            int courseIdToCheck = quiz.IsFinalExam && quiz.CourseId.HasValue ? quiz.CourseId.Value : (quiz.Lesson?.CourseId ?? 0);

            bool isSubscribed = await _context.SubscriptionRequests
                .AnyAsync(r => r.StudentId == studentId && r.CourseId == courseIdToCheck && r.Status == "Approved");

            if (!isSubscribed) return Unauthorized("غير مصرح لك بدخول هذا الاختبار.");

            return View(quiz);
        }

        public async Task<IActionResult> Results()
        {
            int studentId = GetCurrentStudentId();
            var scores = await _context.Set<StudentScore>()
                .Include(s => s.Quiz)
                .Where(s => s.StudentId == studentId)
                .OrderByDescending(s => s.DateTaken)
                .ToListAsync();

            return View(scores);
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            int studentId = GetCurrentStudentId();
            var student = await _context.Users.FindAsync(studentId);
            return View(student);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string FullName, string PhoneNumber, string NewPassword)
        {
            int studentId = GetCurrentStudentId();
            var student = await _context.Users.FindAsync(studentId);

            if (student != null)
            {
                student.FullName = FullName;
                student.PhoneNumber = PhoneNumber;
                student.Username = PhoneNumber;

                if (!string.IsNullOrEmpty(NewPassword))
                {
                    student.PasswordHash = _passwordHasher.HashPassword(student, NewPassword);
                }

                _context.Update(student);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تحديث بياناتك بنجاح!";
            }
            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        public async Task<IActionResult> Certificate(string serial, int? courseId)
        {
            int studentId = GetCurrentStudentId();
            Certificate cert = null;

            if (!string.IsNullOrEmpty(serial))
            {
                cert = await _context.Certificates
                    .Include(c => c.Student)
                    .Include(c => c.Course)
                    .FirstOrDefaultAsync(c => c.SerialNumber == serial);
            }
            else if (courseId.HasValue)
            {
                cert = await _context.Certificates
                    .Include(c => c.Student)
                    .Include(c => c.Course)
                    .FirstOrDefaultAsync(c => c.StudentId == studentId && c.CourseId == courseId.Value);
            }

            if (cert == null) return NotFound("الشهادة غير موجودة أو لم تجتز الاختبار النهائي بعد.");

            return View(cert);
        }
    }
}