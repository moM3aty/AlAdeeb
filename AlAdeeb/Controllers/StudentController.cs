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
    public class LeaderboardDto { public string StudentName { get; set; } public double AverageScore { get; set; } public int TotalExamsTaken { get; set; } }

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

        private int GetCurrentStudentId() { return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)); }

        public async Task<IActionResult> Dashboard()
        {
            int studentId = GetCurrentStudentId();
            var requests = await _context.SubscriptionRequests.Include(r => r.Course).ThenInclude(c => c.Quizzes).Include(r => r.Course).ThenInclude(c => c.Lessons).ThenInclude(l => l.Quizzes).Where(r => r.StudentId == studentId).OrderByDescending(r => r.RequestDate).ToListAsync();
            var passedScores = await _context.StudentScores.Where(s => s.StudentId == studentId && s.Passed).Select(s => s.QuizId).Distinct().ToListAsync();
            var progressDict = new Dictionary<int, double>();

            foreach (var req in requests)
            {
                if (req.Course == null) continue;
                int totalQuizzes = req.Course.Quizzes?.Count ?? 0;
                int passedQuizzes = req.Course.Quizzes?.Count(q => passedScores.Contains(q.Id)) ?? 0;
                if (req.Course.Lessons != null) { foreach (var l in req.Course.Lessons) { totalQuizzes += l.Quizzes?.Count ?? 0; passedQuizzes += l.Quizzes?.Count(q => passedScores.Contains(q.Id)) ?? 0; } }
                double prog = totalQuizzes > 0 ? ((double)passedQuizzes / totalQuizzes) * 100 : 0;
                if (totalQuizzes == 0 && req.Course.Lessons != null && req.Course.Lessons.Any()) prog = 10;
                progressDict[req.Course.Id] = Math.Round(prog, 0);
            }

            ViewBag.ProgressDict = progressDict;
            ViewBag.StudentName = User.Identity.Name;
            return View(requests);
        }

        [AllowAnonymous]
        public async Task<IActionResult> BrowseCourses()
        {
            var courses = await _context.Courses.Where(c => c.IsActive).OrderByDescending(c => c.Id).ToListAsync();
            ViewBag.PlatformSettings = await _context.PlatformSettings.FirstOrDefaultAsync();

            if (User.Identity.IsAuthenticated && User.IsInRole("Student"))
            {
                int studentId = GetCurrentStudentId();
                ViewBag.MySubscriptions = await _context.SubscriptionRequests.Where(r => r.StudentId == studentId).Select(r => r.CourseId).ToListAsync();
            }
            else { ViewBag.MySubscriptions = new List<int>(); }

            return View(courses);
        }

        [AllowAnonymous]
        public async Task<IActionResult> CourseContent(int id)
        {
            int studentId = 0;
            if (User.Identity.IsAuthenticated && User.IsInRole("Student"))
            {
                studentId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            }

            var subscription = await _context.SubscriptionRequests.FirstOrDefaultAsync(r => r.StudentId == studentId && r.CourseId == id && r.Status == "Approved");

            bool hasAccess = subscription != null && (!subscription.ExpiryDate.HasValue || subscription.ExpiryDate.Value > DateTime.Now);
            ViewBag.IsExpired = !hasAccess;

            var course = await _context.Courses.Include(c => c.Lessons).ThenInclude(l => l.Materials).Include(c => c.Lessons).ThenInclude(l => l.Quizzes).Include(c => c.Quizzes).FirstOrDefaultAsync(c => c.Id == id);
            if (course == null) return NotFound();

            ViewBag.LiveSessions = await _context.Set<LiveSession>().Where(l => l.CourseId == id).OrderByDescending(l => l.ScheduledDate).ToListAsync();

            var passedScores = await _context.StudentScores.Where(s => s.StudentId == studentId && s.Passed).Select(s => s.QuizId).ToListAsync();
            var unlockedLessonIds = new List<int>();
            bool previousLessonPassed = true;
            int totalQCount = course.Quizzes?.Count ?? 0;
            int passedQCount = course.Quizzes?.Count(q => passedScores.Contains(q.Id)) ?? 0;

            foreach (var lesson in course.Lessons.OrderBy(l => l.OrderIndex))
            {
                if (previousLessonPassed) unlockedLessonIds.Add(lesson.Id);
                if (lesson.Quizzes != null && lesson.Quizzes.Any())
                {
                    previousLessonPassed = lesson.Quizzes.All(q => passedScores.Contains(q.Id));
                    totalQCount += lesson.Quizzes.Count;
                    passedQCount += lesson.Quizzes.Count(q => passedScores.Contains(q.Id));
                }
            }

            ViewBag.UnlockedLessonIds = unlockedLessonIds;
            double progress = totalQCount > 0 ? ((double)passedQCount / totalQCount) * 100 : 0;
            if (totalQCount == 0 && course.Lessons.Any()) progress = 10;
            ViewBag.ProgressPercentage = Math.Round(progress, 0);

            var leaderboard = await _context.StudentScores.Include(s => s.Student).Include(s => s.Quiz).ThenInclude(q => q.Lesson).Where(s => s.Passed && (s.Quiz.CourseId == id || (s.Quiz.Lesson != null && s.Quiz.Lesson.CourseId == id))).GroupBy(s => new { s.StudentId, s.Student.FullName }).Select(g => new LeaderboardDto { StudentName = g.Key.FullName, AverageScore = Math.Round(g.Average(s => s.ScorePercentage), 1), TotalExamsTaken = g.Count() }).OrderByDescending(x => x.AverageScore).ThenByDescending(x => x.TotalExamsTaken).Take(10).ToListAsync();
            ViewBag.Leaderboard = leaderboard;

            return View(course);
        }

        [HttpGet]
        public async Task<IActionResult> TakeQuiz(int id)
        {
            var quiz = await _context.Quizzes.Include(q => q.Questions).FirstOrDefaultAsync(q => q.Id == id);
            if (quiz == null) return NotFound();

            if (quiz.IsSimulator)
            {
                int courseId = quiz.CourseId ?? (quiz.Lesson != null ? quiz.Lesson.CourseId : 0);
                var randomSections = await _context.QuestionBankSections.Include(s => s.Questions).Where(s => s.CourseId == courseId).OrderBy(x => Guid.NewGuid()).Take(quiz.SimulatorSectionsCount).ToListAsync();
                var simulatedQuestions = new List<Question>();
                foreach (var sec in randomSections) { foreach (var bq in sec.Questions) { simulatedQuestions.Add(new Question { Id = bq.Id, SkillType = bq.SkillType, QuestionText = bq.QuestionText, QuestionImagePath = bq.QuestionImagePath, OptionA = bq.OptionA, OptionB = bq.OptionB, OptionC = bq.OptionC, OptionD = bq.OptionD, CorrectOption = bq.CorrectOption }); } }
                quiz.Questions = simulatedQuestions.OrderBy(x => Guid.NewGuid()).ToList();
            }
            return View(quiz);
        }

        public async Task<IActionResult> Results()
        {
            int studentId = GetCurrentStudentId();
            var scores = await _context.StudentScores.Include(s => s.Quiz).ThenInclude(q => q.Course).Where(s => s.StudentId == studentId).OrderByDescending(s => s.DateTaken).ToListAsync();
            return View(scores);
        }

        [HttpGet] public async Task<IActionResult> Subscribe(int id) { var course = await _context.Courses.FindAsync(id); if (course == null) return NotFound(); return View(course); }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitSubscription(int courseId, IFormFile receiptImage)
        {
            int studentId = GetCurrentStudentId();
            var existing = await _context.SubscriptionRequests.FirstOrDefaultAsync(r => r.CourseId == courseId && r.StudentId == studentId);
            if (existing != null) { TempData["ErrorMessage"] = "لديك طلب اشتراك مسبق في هذه الدورة."; return RedirectToAction(nameof(Dashboard)); }

            var request = new SubscriptionRequest { StudentId = studentId, CourseId = courseId, Status = "Pending", RequestDate = DateTime.Now };

            if (receiptImage != null && receiptImage.Length > 0)
            {
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/receipts");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + receiptImage.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create)) { await receiptImage.CopyToAsync(fileStream); }
                request.ReceiptImagePath = "/uploads/receipts/" + uniqueFileName;
            }
            _context.SubscriptionRequests.Add(request); await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم رفع طلب الاشتراك بنجاح، يرجى انتظار التفعيل من الإدارة.";
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubscribeToBundle()
        {
            int studentId = GetCurrentStudentId();
            var settings = await _context.PlatformSettings.FirstOrDefaultAsync();
            if (settings == null || !settings.IsBundleActive) return RedirectToAction(nameof(BrowseCourses));

            var allCourses = await _context.Courses.Where(c => c.IsActive).ToListAsync();
            foreach (var c in allCourses)
            {
                if (!await _context.SubscriptionRequests.AnyAsync(r => r.CourseId == c.Id && r.StudentId == studentId))
                {
                    _context.SubscriptionRequests.Add(new SubscriptionRequest { StudentId = studentId, CourseId = c.Id, Status = "Pending", RequestDate = DateTime.Now });
                }
            }
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم رفع طلب اشتراكك في الباقة الشاملة بنجاح!";
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpGet] public async Task<IActionResult> Profile() { var studentId = GetCurrentStudentId(); var user = await _context.Users.FindAsync(studentId); return View(user); }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string FullName, string PhoneNumber, string NewPassword)
        {
            var studentId = GetCurrentStudentId();
            var user = await _context.Users.FindAsync(studentId);
            if (user != null)
            {
                user.FullName = FullName; user.PhoneNumber = PhoneNumber; user.Username = PhoneNumber;
                if (!string.IsNullOrWhiteSpace(NewPassword)) { user.PasswordHash = _passwordHasher.HashPassword(user, NewPassword); }
                _context.Update(user); await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تحديث بياناتك بنجاح!";
            }
            return RedirectToAction(nameof(Profile));
        }
    }
}