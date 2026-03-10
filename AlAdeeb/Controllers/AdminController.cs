using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
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
    [Authorize(Roles = "Admin,Teacher")]
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
                var teacherCourses = await _context.Courses.Where(c => c.TeacherId == currentUserId).OrderByDescending(c => c.Id).ToListAsync();
                return View(teacherCourses);
            }
        }

        // ==========================================
        // إدارة المعلمين
        // ==========================================
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
        // إدارة الكورسات (مُحدثة لحل مشكلة قائمة المعلمين)
        // ==========================================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCourse()
        {
            // إرسال قائمة المعلمين للواجهة
            ViewBag.Teachers = await _context.Users.Where(u => u.Role == "Teacher").ToListAsync();
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCourse(Course model, IFormFile courseImage)
        {
            ModelState.Remove("Lessons");
            ModelState.Remove("Quizzes");
            ModelState.Remove("Teacher");
            ModelState.Remove("QuestionBankSections");
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

            // في حال وجود خطأ بالبيانات، يجب إعادة إرسال القائمة حتى لا تختفي
            ViewBag.Teachers = await _context.Users.Where(u => u.Role == "Teacher").ToListAsync();
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditCourse(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            // إرسال قائمة المعلمين للواجهة
            ViewBag.Teachers = await _context.Users.Where(u => u.Role == "Teacher").ToListAsync();
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

            // إعادة إرسال القائمة في حال فشل الـ Validation
            ViewBag.Teachers = await _context.Users.Where(u => u.Role == "Teacher").ToListAsync();
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

        // ==========================================
        // إدارة الطلاب والأجهزة
        // ==========================================
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

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var student = await _context.Users.FindAsync(id);
            if (student != null)
            {
                // مسح كافة سجلات الطالب بشكل متسلسل لتجنب أخطاء قواعد البيانات
                var subscriptions = _context.SubscriptionRequests.Where(s => s.StudentId == id);
                _context.SubscriptionRequests.RemoveRange(subscriptions);

                var scores = _context.StudentScores.Where(s => s.StudentId == id);
                _context.StudentScores.RemoveRange(scores);

                var certificates = _context.Certificates.Where(c => c.StudentId == id);
                _context.Certificates.RemoveRange(certificates);

                var forumReplies = _context.ForumReplies.Where(r => r.UserId == id);
                _context.ForumReplies.RemoveRange(forumReplies);

                var forumPosts = _context.ForumPosts.Include(p => p.Replies).Where(p => p.UserId == id);
                foreach (var post in forumPosts)
                {
                    _context.ForumReplies.RemoveRange(post.Replies);
                }
                _context.ForumPosts.RemoveRange(forumPosts);

                _context.Users.Remove(student);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف الطالب وكافة بياناته المرتبطة بنجاح.";
            }
            return RedirectToAction(nameof(StudentsList));
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult AddStudentManual()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
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

            var passwordHasher = new PasswordHasher<ApplicationUser>();
            newUser.PasswordHash = passwordHasher.HashPassword(newUser, Password);

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تسجيل الطالب بنجاح! يمكنك الآن تفعيل الدورات له.";
            return RedirectToAction(nameof(StudentsList));
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
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

        // ==========================================
        // إدارة الاشتراكات
        // ==========================================
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
        // إدارة بنك الأسئلة (Question Bank)
        // ==========================================
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

        [HttpGet]
        public async Task<IActionResult> EditQuiz(int id)
        {
            var quiz = await _context.Quizzes.Include(q => q.Questions).FirstOrDefaultAsync(q => q.Id == id);
            if (quiz == null) return NotFound();
            return View(quiz);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuiz(int id, Quiz model, List<Question> Questions)
        {
            if (id != model.Id) return NotFound();

            var existingQuiz = await _context.Quizzes.Include(q => q.Questions).Include(q => q.Lesson).FirstOrDefaultAsync(q => q.Id == id);
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
                        using (var fileStream = new FileStream(filePath, FileMode.Create)) { await imageFile.CopyToAsync(fileStream); }
                        q.QuestionImagePath = "/uploads/questions/" + uniqueFileName;
                    }
                    else
                    {
                        var existingImage = Request.Form[$"Questions[{i}].ExistingImagePath"];
                        q.QuestionImagePath = existingImage;
                    }

                    q.QuestionText = q.QuestionText ?? ""; q.OptionA = q.OptionA ?? ""; q.OptionB = q.OptionB ?? ""; q.OptionC = q.OptionC ?? ""; q.OptionD = q.OptionD ?? ""; q.CorrectOption = q.CorrectOption ?? "A"; q.QuizId = existingQuiz.Id;
                    _context.Questions.Add(q);
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث بيانات الاختبار بنجاح!";
            return RedirectToAction("Details", "Course", new { id = existingQuiz.CourseId ?? existingQuiz.Lesson?.CourseId });
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

                settings.IsBundleActive = Request.Form["IsBundleActive"] == "true";
                settings.BundleTitle = model.BundleTitle;
                settings.BundleDescription = model.BundleDescription;
                settings.BundlePrice = model.BundlePrice;
                settings.BundleOldPrice = model.BundleOldPrice;
                settings.BundleDurationMonths = model.BundleDurationMonths;
                settings.TrainerBio = model.TrainerBio;

                _context.Update(settings);
            }
            else
            {
                model.IsBundleActive = Request.Form["IsBundleActive"] == "true";
                _context.PlatformSettings.Add(model);
            }
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث إعدادات الشاشة الرئيسية والباقة الشاملة بنجاح.";
            return RedirectToAction(nameof(PlatformSettings));
        }
    }
}