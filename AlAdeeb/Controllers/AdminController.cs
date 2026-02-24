using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using AlAdeeb.Data;
using AlAdeeb.Models;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System;
using System.Collections.Generic;

namespace AlAdeeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.PendingRequestsCount = await _context.SubscriptionRequests.CountAsync(r => r.Status == "Pending");
            ViewBag.TotalStudentsCount = await _context.Users.CountAsync(u => u.Role == "Student");
            ViewBag.TotalCoursesCount = await _context.Courses.CountAsync();

            var courses = await _context.Courses.OrderByDescending(c => c.Id).ToListAsync();
            return View(courses);
        }

        [HttpGet]
        public IActionResult CreateCourse()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCourse(Course model, IFormFile courseImage)
        {
            ModelState.Remove("Lessons");
            ModelState.Remove("Quizzes");
            if (ModelState.IsValid)
            {
                if (courseImage != null)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads/courses");
                    Directory.CreateDirectory(uploadsFolder);
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + courseImage.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await courseImage.CopyToAsync(fileStream);
                    }
                    model.ImageUrl = "/uploads/courses/" + uniqueFileName;
                }

                _context.Courses.Add(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إنشاء الكورس بنجاح!";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditCourse(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();
            return View(course);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCourse(int id, Course model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تعديل الكورس بنجاح!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CourseExists(model.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course != null)
            {
                _context.Courses.Remove(course);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف الكورس بنجاح.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool CourseExists(int id)
        {
            return _context.Courses.Any(e => e.Id == id);
        }

        public async Task<IActionResult> SubscriptionRequests()
        {
            var requests = await _context.SubscriptionRequests
                .Include(r => r.Student)
                .Include(r => r.Course)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }
        [HttpGet]
        public async Task<IActionResult> StudentProfile(int id)
        {
            var student = await _context.Users.FindAsync(id);
            if (student == null || student.Role != "Student") return NotFound("الطالب غير موجود.");

            ViewBag.Subscriptions = await _context.SubscriptionRequests
                .Include(s => s.Course)
                .Where(s => s.StudentId == id)
                .OrderByDescending(s => s.RequestDate)
                .ToListAsync();

            var scores = await _context.StudentScores
                .Include(s => s.Quiz)
                .Where(s => s.StudentId == id)
                .OrderByDescending(s => s.DateTaken)
                .ToListAsync();

            ViewBag.Scores = scores;
            ViewBag.AverageScore = scores.Any() ? Math.Round(scores.Average(s => s.ScorePercentage), 1) : 0;

            ViewBag.Certificates = await _context.Certificates
                .Include(c => c.Course)
                .Where(c => c.StudentId == id)
                .OrderByDescending(c => c.IssueDate)
                .ToListAsync();

            return View(student);
        }

        [HttpGet]
        public async Task<IActionResult> ViewCertificate(string serial)
        {
            var cert = await _context.Certificates
                .Include(c => c.Student)
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.SerialNumber == serial);

            if (cert == null) return NotFound("الشهادة غير موجودة.");

            // يتم عرض ملف الشهادة الموجود في مجلد الطالب ولكن من خلال أكشن المدير
            return View("~/Views/Student/Certificate.cshtml", cert);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSubscriptionStatus(int requestId, string status)
        {
            var request = await _context.SubscriptionRequests.FindAsync(requestId);
            if (request != null)
            {
                request.Status = status;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = status == "Approved" ? "تم تفعيل حساب الطالب بنجاح!" : "تم رفض الطلب.";
            }
            return RedirectToAction(nameof(SubscriptionRequests));
        }

        public async Task<IActionResult> StudentsList()
        {
            var students = await _context.Users
                .Where(u => u.Role == "Student")
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
            return View(students);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var student = await _context.Users.FindAsync(id);
            if (student != null)
            {
                _context.Users.Remove(student);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف حساب الطالب وبياناته بالكامل.";
            }
            return RedirectToAction(nameof(StudentsList));
        }

        [HttpGet]
        public async Task<IActionResult> AddQuiz(int courseId, int? lessonId, bool isFinalExam = false)
        {
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound("الكورس غير موجود.");

            ViewBag.LessonId = lessonId;
            ViewBag.IsFinalExam = isFinalExam;

            return View(course);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveQuiz(int CourseId, int? LessonId, string QuizTitle, int DurationInMinutes, bool IsFinalExam, List<Question> Questions)
        {
            if (Questions == null) Questions = new List<Question>();

            var newQuiz = new Quiz
            {
                CourseId = IsFinalExam ? CourseId : null,
                LessonId = IsFinalExam ? null : LessonId,
                Title = string.IsNullOrEmpty(QuizTitle) ? "اختبار جديد" : QuizTitle,
                DurationInMinutes = DurationInMinutes,
                IsFinalExam = IsFinalExam,
                Questions = new List<Question>()
            };

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
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }
                    q.QuestionImagePath = "/uploads/questions/" + uniqueFileName;
                }
                else
                {
                    q.QuestionImagePath = "";
                }

                q.QuestionText = q.QuestionText ?? "";
                q.OptionA = q.OptionA ?? "";
                q.OptionB = q.OptionB ?? "";
                q.OptionC = q.OptionC ?? "";
                q.OptionD = q.OptionD ?? "";
                q.CorrectOption = q.CorrectOption ?? "A";

                if (string.IsNullOrEmpty(q.QuestionText) && !string.IsNullOrEmpty(q.QuestionImagePath))
                {
                    q.QuestionText = "[سؤال مصور]";
                }

                newQuiz.Questions.Add(q);
            }

            _context.Quizzes.Add(newQuiz);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حفظ الاختبار بنجاح!";
            return RedirectToAction("Details", "Course", new { id = CourseId });
        }
        [HttpGet]
        public async Task<IActionResult> EditQuiz(int id)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quiz == null) return NotFound();

            return View(quiz);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuiz(int id, Quiz model, List<Question> Questions)
        {
            if (id != model.Id) return NotFound();

            var existingQuiz = await _context.Quizzes
                .Include(q => q.Questions)
                .Include(q => q.Lesson)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (existingQuiz == null) return NotFound();

            existingQuiz.Title = string.IsNullOrEmpty(model.Title) ? "اختبار" : model.Title;
            existingQuiz.DurationInMinutes = model.DurationInMinutes;
            existingQuiz.MinimumPassScore = model.MinimumPassScore;

            // مسح الأسئلة القديمة وإضافة الجديدة التي تم إرسالها من الفورم
            _context.Questions.RemoveRange(existingQuiz.Questions);

            if (Questions != null && Questions.Any())
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
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }
                        q.QuestionImagePath = "/uploads/questions/" + uniqueFileName;
                    }
                    else
                    {
                        var existingImage = Request.Form[$"Questions[{i}].ExistingImagePath"];
                        q.QuestionImagePath = existingImage;
                    }

                    q.QuestionText = q.QuestionText ?? "";
                    q.OptionA = q.OptionA ?? "";
                    q.OptionB = q.OptionB ?? "";
                    q.OptionC = q.OptionC ?? "";
                    q.OptionD = q.OptionD ?? "";
                    q.CorrectOption = q.CorrectOption ?? "A";
                    q.QuizId = existingQuiz.Id;

                    if (string.IsNullOrEmpty(q.QuestionText) && !string.IsNullOrEmpty(q.QuestionImagePath))
                    {
                        q.QuestionText = "[سؤال مصور]";
                    }

                    _context.Questions.Add(q);
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث بيانات الاختبار بنجاح!";

            return RedirectToAction("Details", "Course", new { id = existingQuiz.CourseId ?? existingQuiz.Lesson?.CourseId });
        }
    }
}