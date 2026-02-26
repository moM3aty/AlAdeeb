using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity; // تم إضافة المكتبة هنا لحل خطأ التشفير
using AlAdeeb.Data;
using AlAdeeb.Models;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System;
using System.Collections.Generic;
using System.Security.Claims;

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
            bool isAdmin = User.IsInRole("Admin");
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            if (isAdmin)
            {
                ViewBag.PendingRequestsCount = await _context.SubscriptionRequests.CountAsync(r => r.Status == "Pending");
                ViewBag.TotalStudentsCount = await _context.Users.CountAsync(u => u.Role == "Student");
                ViewBag.TotalCoursesCount = await _context.Courses.CountAsync();
                var courses = await _context.Courses.OrderByDescending(c => c.Id).ToListAsync();
                return View(courses);
            }
            else
            {
                // المعلم يرى فقط كورساته
                var teacherCourses = await _context.Courses.Where(c => c.TeacherId == currentUserId).OrderByDescending(c => c.Id).ToListAsync();
                return View(teacherCourses);
            }
        }
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> TeachersList()
        {
            var teachers = await _context.Users.Where(u => u.Role == "Teacher").ToListAsync();
            return View(teachers);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTeacher(string FullName, string PhoneNumber, string Password)
        {
            if (await _context.Users.AnyAsync(u => u.PhoneNumber == PhoneNumber))
            {
                TempData["ErrorMessage"] = "رقم الجوال مستخدم بالفعل.";
                return RedirectToAction(nameof(TeachersList));
            }

            var passwordHasher = new PasswordHasher<ApplicationUser>();
            var teacher = new ApplicationUser
            {
                FullName = FullName,
                Username = PhoneNumber,
                PhoneNumber = PhoneNumber,
                Role = "Teacher",
                CreatedAt = DateTime.Now
            };
            teacher.PasswordHash = passwordHasher.HashPassword(teacher, Password);

            _context.Users.Add(teacher);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة المعلم بنجاح.";
            return RedirectToAction(nameof(TeachersList));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTeacher(int id)
        {
            var teacher = await _context.Users.FindAsync(id);
            if (teacher != null)
            {
                _context.Users.Remove(teacher);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف المعلم بنجاح.";
            }
            return RedirectToAction(nameof(TeachersList));
        }
        // ==========================================
        // إدارة الكورسات
        // ==========================================
        [HttpGet]
        [Authorize(Roles = "Admin")] // المدير فقط ينشئ كورس
        public IActionResult CreateCourse() => View();

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCourse(Course model, IFormFile courseImage)
        {
            if (ModelState.IsValid)
            {
                if (courseImage != null)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads/courses");
                    Directory.CreateDirectory(uploadsFolder);
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + courseImage.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create)) { await courseImage.CopyToAsync(fileStream); }
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditCourse(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();
            ViewBag.Teachers = await _context.Users.Where(u => u.Role == "Teacher").ToListAsync(); // لجلب المعلمين في الفيو
            return View(course);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCourse(int id, Course model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تعديل الكورس وتعيين المعلم بنجاح!";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> StudentsList()
        {
            var students = await _context.Users.Where(u => u.Role == "Student").OrderByDescending(u => u.CreatedAt).ToListAsync();
            return View(students);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDeviceLimit(int studentId, int newLimit)
        {
            var student = await _context.Users.FindAsync(studentId);
            if (student != null)
            {
                student.AllowedDevicesCount = newLimit;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تحديث حد الأجهزة المسموحة للطالب بنجاح.";
            }
            return RedirectToAction(nameof(StudentsList));
        }
        // ==========================================
        // إدارة الاشتراكات
        // ==========================================
        public async Task<IActionResult> SubscriptionRequests()
        {
            var requests = await _context.SubscriptionRequests
                .Include(r => r.Student)
                .Include(r => r.Course)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            ViewBag.PendingCount = requests.Count(r => r.Status == "Pending");
            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSubscriptionStatus(int requestId, string status)
        {
            var request = await _context.SubscriptionRequests.Include(r => r.Course).FirstOrDefaultAsync(r => r.Id == requestId);
            if (request != null)
            {
                request.Status = status;
                if (status == "Approved")
                {
                    if (request.Course.AccessDurationMonths.HasValue && request.Course.AccessDurationMonths.Value > 0)
                        request.ExpiryDate = DateTime.Now.AddMonths(request.Course.AccessDurationMonths.Value);
                    else
                        request.ExpiryDate = null;

                    TempData["SuccessMessage"] = "تم تفعيل حساب الطالب بنجاح!";
                }
                else
                {
                    TempData["SuccessMessage"] = "تم إيقاف/رفض الطلب.";
                }
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(SubscriptionRequests));
        }

        // ==========================================
        // إدارة الطلاب
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var student = await _context.Users.FindAsync(id);
            if (student != null)
            {
                _context.Users.Remove(student);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف الطالب.";
            }
            return RedirectToAction(nameof(StudentsList));
        }

        // --- إضافة الطالب يدوياً ---
        [HttpGet]
        public IActionResult AddStudentManual()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStudentManual(string FullName, string PhoneNumber, string Password)
        {
            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(PhoneNumber) || string.IsNullOrWhiteSpace(Password))
            {
                ModelState.AddModelError("", "جميع الحقول مطلوبة");
                return View();
            }

            if (await _context.Users.AnyAsync(u => u.PhoneNumber == PhoneNumber))
            {
                ModelState.AddModelError("", "رقم الجوال مسجل مسبقاً في المنصة.");
                return View();
            }

            var newUser = new ApplicationUser
            {
                FullName = FullName,
                Username = PhoneNumber,
                PhoneNumber = PhoneNumber,
                Role = "Student",
                CreatedAt = DateTime.Now,
                AllowedDevicesCount = 1
            };

            // تعريف أداة التشفير محلياً لحل الخطأ
            var passwordHasher = new PasswordHasher<ApplicationUser>();
            newUser.PasswordHash = passwordHasher.HashPassword(newUser, Password);

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تسجيل الطالب بنجاح! يمكنك الآن تفعيل الدورات له.";
            return RedirectToAction(nameof(StudentsList));
        }

        [HttpGet]
        public async Task<IActionResult> StudentProfile(int id)
        {
            var student = await _context.Users.FindAsync(id);
            if (student == null || student.Role != "Student") return NotFound("الطالب غير موجود.");

            ViewBag.Subscriptions = await _context.SubscriptionRequests.Include(s => s.Course).Where(s => s.StudentId == id).OrderByDescending(s => s.RequestDate).ToListAsync();
            var scores = await _context.StudentScores.Include(s => s.Quiz).Where(s => s.StudentId == id).OrderByDescending(s => s.DateTaken).ToListAsync();
            ViewBag.Scores = scores;
            ViewBag.AverageScore = scores.Any() ? Math.Round(scores.Average(s => s.ScorePercentage), 1) : 0;
            ViewBag.Certificates = await _context.Certificates.Include(c => c.Course).Where(c => c.StudentId == id).OrderByDescending(c => c.IssueDate).ToListAsync();
            return View(student);
        }

        [HttpGet]
        public async Task<IActionResult> ViewCertificate(string serial)
        {
            var cert = await _context.Certificates.Include(c => c.Student).Include(c => c.Course).FirstOrDefaultAsync(c => c.SerialNumber == serial);
            if (cert == null) return NotFound();
            return View("~/Views/Student/Certificate.cshtml", cert);
        }

        // ==========================================
        // إدارة المنتدى (النقاشات)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Discussions(int? courseId)
        {
            ViewBag.Courses = await _context.Courses.ToListAsync();
            ViewBag.SelectedCourseId = courseId;

            var query = _context.ForumPosts
                .Include(p => p.User)
                .Include(p => p.Course)
                .Include(p => p.Replies)
                    .ThenInclude(r => r.User)
                .AsQueryable();

            if (courseId.HasValue && courseId.Value > 0)
            {
                query = query.Where(p => p.CourseId == courseId.Value);
            }

            var posts = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
            return View(posts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminReply(int postId, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return RedirectToAction(nameof(Discussions));

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var reply = new ForumReply
            {
                ForumPostId = postId,
                UserId = userId,
                Content = content,
                CreatedAt = DateTime.Now
            };

            _context.ForumReplies.Add(reply);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة الرد بنجاح.";
            return RedirectToAction(nameof(Discussions));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteForumPost(int postId)
        {
            var post = await _context.ForumPosts.FindAsync(postId);
            if (post != null)
            {
                _context.ForumPosts.Remove(post);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف المنشور لمخالفته القواعد.";
            }
            return RedirectToAction(nameof(Discussions));
        }

        // ==========================================
        // إدارة الاختبارات (Quizzes)
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
        public async Task<IActionResult> SaveQuiz(int CourseId, int? LessonId, string QuizTitle, int DurationInMinutes, bool IsFinalExam, List<Question> Questions)
        {
            if (Questions == null) Questions = new List<Question>();
            var newQuiz = new Quiz { CourseId = IsFinalExam ? CourseId : null, LessonId = IsFinalExam ? null : LessonId, Title = string.IsNullOrEmpty(QuizTitle) ? "اختبار جديد" : QuizTitle, DurationInMinutes = DurationInMinutes, IsFinalExam = IsFinalExam, Questions = new List<Question>() };
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
                q.QuestionText = q.QuestionText ?? "";
                q.OptionA = q.OptionA ?? ""; q.OptionB = q.OptionB ?? ""; q.OptionC = q.OptionC ?? ""; q.OptionD = q.OptionD ?? "";
                q.CorrectOption = q.CorrectOption ?? "A";
                q.SkillType = q.SkillType ?? "أسئلة منوعة"; // حفظ نوع المهارة للمحاكي
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