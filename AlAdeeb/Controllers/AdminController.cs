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

namespace AlAdeeb.Controllers
{
    [Authorize(Roles = "Admin")] // حماية كاملة: لا يدخل إلا الأدمن
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // ==========================================
        // 1. لوحة التحكم (Dashboard & Read Courses)
        // ==========================================
        public async Task<IActionResult> Index()
        {
            ViewBag.PendingRequestsCount = await _context.SubscriptionRequests.CountAsync(r => r.Status == "Pending");
            ViewBag.TotalStudentsCount = await _context.Users.CountAsync(u => u.Role == "Student");
            ViewBag.TotalCoursesCount = await _context.Courses.CountAsync();

            var courses = await _context.Courses.OrderByDescending(c => c.Id).ToListAsync();
            return View(courses);
        }

        // ==========================================
        // 2. إدارة الكورسات (Create, Edit, Delete)
        // ==========================================

        [HttpGet]
        public IActionResult CreateCourse()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCourse(Course model, IFormFile courseImage)
        {
            if (ModelState.IsValid)
            {
                // حفظ صورة الكورس إذا تم رفعها
                // ملاحظة: تأكد من إضافة حقل ImageUrl في موديل الـ Course
                // if (courseImage != null)
                // {
                //     string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads/courses");
                //     Directory.CreateDirectory(uploadsFolder);
                //     string uniqueFileName = Guid.NewGuid().ToString() + "_" + courseImage.FileName;
                //     string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                //     using (var fileStream = new FileStream(filePath, FileMode.Create))
                //     {
                //         await courseImage.CopyToAsync(fileStream);
                //     }
                //     model.ImageUrl = "/uploads/courses/" + uniqueFileName;
                // }

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

        // ==========================================
        // 3. إدارة الاشتراكات (Read, Update Status)
        // ==========================================
        public async Task<IActionResult> SubscriptionRequests()
        {
            var requests = await _context.SubscriptionRequests
                .Include(r => r.Student)
                .Include(r => r.Course)
                .Where(r => r.Status == "Pending")
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSubscriptionStatus(int requestId, string status)
        {
            var request = await _context.SubscriptionRequests.FindAsync(requestId);
            if (request != null)
            {
                request.Status = status; // "Approved" or "Rejected"
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = status == "Approved" ? "تم تفعيل حساب الطالب بنجاح!" : "تم رفض الطلب.";
            }
            return RedirectToAction(nameof(SubscriptionRequests));
        }

        // ==========================================
        // 4. إدارة الطلاب (Read, Delete)
        // ==========================================
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

        // ==========================================
        // 5. إدارة وبناء الاختبارات (Quizzes)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> AddQuiz(int courseId)
        {
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound("الكورس غير موجود.");
            return View(course);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveQuiz(int CourseId, string QuizTitle, int DurationInMinutes, List<Question> Questions)
        {
            // 1. معالجة القائمة لتجنب NullReferenceException
            if (Questions == null) Questions = new List<Question>();

            var newQuiz = new Quiz
            {
                CourseId = CourseId,
                Title = string.IsNullOrEmpty(QuizTitle) ? "اختبار جديد" : QuizTitle,
                DurationInMinutes = DurationInMinutes,
                Questions = new List<Question>()
            };

            // إنشاء مجلد الصور إذا لم يكن موجوداً
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads/questions");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            // 2. معالجة كل سؤال (النص والـ Snapshot معاً)
            for (int i = 0; i < Questions.Count; i++)
            {
                var q = Questions[i];
                if (q == null) continue;

                // معالجة صورة الـ Snapshot
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
                    q.QuestionImagePath = ""; // ضمان عدم إرسال NULL
                }

                // 3. ضمان تعبئة كافة الحقول لتجنب SqlException
                q.QuestionText = q.QuestionText ?? "";
                q.OptionA = q.OptionA ?? "";
                q.OptionB = q.OptionB ?? "";
                q.OptionC = q.OptionC ?? "";
                q.OptionD = q.OptionD ?? "";
                q.CorrectOption = q.CorrectOption ?? "A";

                // إذا كانت الصورة موجودة والنص فارغ، نضع وسماً نصياً
                if (string.IsNullOrEmpty(q.QuestionText) && !string.IsNullOrEmpty(q.QuestionImagePath))
                {
                    q.QuestionText = "[سؤال مصور]";
                }

                newQuiz.Questions.Add(q);
            }

            _context.Quizzes.Add(newQuiz);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حفظ الاختبار بنجاح! تم دمج النصوص واللقطات المصورة (Snapshots) في بنك الأسئلة.";
            return RedirectToAction("Details", "Course", new { id = CourseId });
        }
    }
}