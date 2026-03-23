using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AlAdeeb.Data;
using AlAdeeb.Models;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace AlAdeeb.Controllers
{
    [Route("api/mobile")]
    [ApiController]
    public class MobileApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MobileApiController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. Auth (المصادقة وتسجيل الدخول)
        // ==========================================
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
            if (user == null) return Unauthorized(new { success = false, message = "رقم الجوال أو كلمة المرور غير صحيحة" });

            var passwordHasher = new PasswordHasher<ApplicationUser>();
            var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if (result == PasswordVerificationResult.Success)
            {
                return Ok(new
                {
                    success = true,
                    message = "تم تسجيل الدخول بنجاح",
                    token = "generate_jwt_token_here_based_on_user_id_" + user.Id,
                    user = new { user.Id, user.FullName, user.PhoneNumber, user.Role }
                });
            }
            return Unauthorized(new { success = false, message = "رقم الجوال أو كلمة المرور غير صحيحة" });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber))
                return BadRequest(new { success = false, message = "رقم الجوال مسجل مسبقاً" });

            var newUser = new ApplicationUser { FullName = request.FullName, Username = request.PhoneNumber, PhoneNumber = request.PhoneNumber, Role = "Student", CreatedAt = DateTime.Now, AllowedDevicesCount = 1 };
            var passwordHasher = new PasswordHasher<ApplicationUser>();
            newUser.PasswordHash = passwordHasher.HashPassword(newUser, request.Password);

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم التسجيل بنجاح" });
        }

        [HttpPost("profile/update")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null) return NotFound(new { success = false, message = "المستخدم غير موجود" });

            user.FullName = request.FullName;
            user.PhoneNumber = request.PhoneNumber;
            user.Username = request.PhoneNumber;

            if (!string.IsNullOrWhiteSpace(request.NewPassword))
            {
                var passwordHasher = new PasswordHasher<ApplicationUser>();
                user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
            }

            _context.Update(user);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم تحديث البيانات بنجاح" });
        }

        // ==========================================
        // 2. Home & Browse (الرئيسية واستكشاف الكورسات)
        // ==========================================
        [HttpGet("home")]
        public async Task<IActionResult> GetHomeData()
        {
            var settings = await _context.PlatformSettings.FirstOrDefaultAsync();
            var activeCourses = await _context.Courses
                .Where(c => c.IsActive)
                .Select(c => new { c.Id, c.Title, c.Description, c.ImageUrl, c.Price, c.OldPrice, c.TrainerName })
                .ToListAsync();

            return Ok(new { success = true, settings, courses = activeCourses });
        }

        [HttpGet("course/{id}")]
        public async Task<IActionResult> GetCourseDetails(int id)
        {
            var course = await _context.Courses
                .Include(c => c.Lessons).ThenInclude(l => l.Materials)
                .Include(c => c.Lessons).ThenInclude(l => l.Quizzes)
                .Select(c => new {
                    c.Id,
                    c.Title,
                    c.Description,
                    c.ImageUrl,
                    c.Price,
                    c.OldPrice,
                    c.TrainerName,
                    c.TrainerBio,
                    LessonsCount = c.Lessons.Count,
                    Lessons = c.Lessons.OrderBy(l => l.OrderIndex).Select(l => new {
                        l.Id,
                        l.Title,
                        Materials = l.Materials.OrderBy(m => m.OrderIndex).Select(m => new { m.Id, m.Title, m.MaterialType, m.IsFreeSample }),
                        Quizzes = l.Quizzes.Select(q => new { q.Id, q.Title, q.DurationInMinutes, q.IsFinalExam })
                    })
                })
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return NotFound(new { success = false, message = "الكورس غير موجود" });
            return Ok(new { success = true, course });
        }

        // ==========================================
        // 3. Subscriptions (الاشتراكات والدفع)
        // ==========================================
        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromForm] int studentId, [FromForm] int courseId, IFormFile receiptImage)
        {
            if (receiptImage == null || receiptImage.Length == 0) return BadRequest(new { success = false, message = "يرجى إرفاق صورة الإيصال" });

            var existing = await _context.SubscriptionRequests.FirstOrDefaultAsync(r => r.CourseId == courseId && r.StudentId == studentId);
            if (existing != null) return BadRequest(new { success = false, message = "لديك طلب اشتراك مسبق في هذه الدورة." });

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + receiptImage.FileName;
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/receipts", uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create)) { await receiptImage.CopyToAsync(fileStream); }

            var request = new SubscriptionRequest { StudentId = studentId, CourseId = courseId, Status = "Pending", RequestDate = DateTime.Now, ReceiptImagePath = "/uploads/receipts/" + uniqueFileName };
            _context.SubscriptionRequests.Add(request);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم رفع الإيصال بنجاح، بانتظار تفعيل الإدارة." });
        }

        [HttpPost("subscribe-bundle")]
        public async Task<IActionResult> SubscribeBundle([FromForm] int studentId, IFormFile receiptImage)
        {
            if (receiptImage == null || receiptImage.Length == 0) return BadRequest(new { success = false, message = "يرجى إرفاق صورة الإيصال" });

            var settings = await _context.PlatformSettings.FirstOrDefaultAsync();
            if (settings == null || !settings.IsBundleActive) return BadRequest(new { success = false, message = "الباقة الشاملة غير مفعلة حالياً." });

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + receiptImage.FileName;
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/receipts", uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create)) { await receiptImage.CopyToAsync(fileStream); }
            string receiptPath = "/uploads/receipts/" + uniqueFileName;

            var allCourses = await _context.Courses.Where(c => c.IsActive).ToListAsync();
            foreach (var c in allCourses)
            {
                if (!await _context.SubscriptionRequests.AnyAsync(r => r.CourseId == c.Id && r.StudentId == studentId))
                {
                    _context.SubscriptionRequests.Add(new SubscriptionRequest { StudentId = studentId, CourseId = c.Id, Status = "Pending", RequestDate = DateTime.Now, ReceiptImagePath = receiptPath });
                }
            }
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم رفع إيصال الباقة الشاملة بنجاح." });
        }

        // ==========================================
        // 4. Learning (قاعة المذاكرة للطالب)
        // ==========================================
        [HttpGet("my-courses/{studentId}")]
        public async Task<IActionResult> GetMyCourses(int studentId)
        {
            var activeSubscriptions = await _context.SubscriptionRequests
                .Include(s => s.Course)
                .Where(s => s.StudentId == studentId && s.Status == "Approved" && (s.ExpiryDate == null || s.ExpiryDate > DateTime.Now))
                .Select(s => new { s.Course.Id, s.Course.Title, s.Course.ImageUrl, s.Course.Description, s.ExpiryDate })
                .ToListAsync();

            return Ok(new { success = true, myCourses = activeSubscriptions });
        }

        [HttpGet("material-url/{studentId}/{materialId}")]
        public async Task<IActionResult> GetMaterialUrl(int studentId, int materialId)
        {
            var material = await _context.LessonMaterials.Include(m => m.Lesson).FirstOrDefaultAsync(m => m.Id == materialId);
            if (material == null) return NotFound(new { success = false, message = "المحتوى غير موجود" });

            if (material.IsFreeSample) return Ok(new { success = true, url = material.UrlOrPath, type = material.MaterialType });

            var hasAccess = await _context.SubscriptionRequests.AnyAsync(s => s.StudentId == studentId && s.CourseId == material.Lesson.CourseId && s.Status == "Approved");
            if (!hasAccess) return Unauthorized(new { success = false, message = "غير مصرح لك بمشاهدة هذا المحتوى" });

            return Ok(new { success = true, url = material.UrlOrPath, type = material.MaterialType });
        }

        [HttpGet("live-sessions/{courseId}")]
        public async Task<IActionResult> GetLiveSessions(int courseId)
        {
            var sessions = await _context.LiveSessions
                .Where(l => l.CourseId == courseId)
                .OrderByDescending(l => l.ScheduledDate)
                .Select(l => new { l.Id, l.Title, l.LiveUrl, l.RecordedVideoUrl, l.ScheduledDate, l.IsCompleted })
                .ToListAsync();

            return Ok(new { success = true, sessions });
        }

        // ==========================================
        // 5. Quizzes & Exams (الاختبارات والمحاكي والنتائج)
        // ==========================================
        [HttpGet("quiz/{quizId}")]
        public async Task<IActionResult> GetQuiz(int quizId)
        {
            var quiz = await _context.Quizzes.Include(q => q.Questions).FirstOrDefaultAsync(q => q.Id == quizId);
            if (quiz == null) return NotFound(new { success = false, message = "الاختبار غير موجود" });

            var questionsList = new List<object>();

            if (quiz.IsSimulator)
            {
                int courseId = quiz.CourseId ?? (quiz.Lesson?.CourseId ?? 0);
                var randomSections = await _context.QuestionBankSections.Include(s => s.Questions)
                    .Where(s => s.CourseId == courseId).OrderBy(x => Guid.NewGuid()).Take(quiz.SimulatorSectionsCount).ToListAsync();

                foreach (var sec in randomSections)
                {
                    foreach (var bq in sec.Questions)
                    {
                        questionsList.Add(new { id = bq.Id, text = bq.QuestionText, image = bq.QuestionImagePath, skill = bq.SkillType, options = new { A = bq.OptionA, B = bq.OptionB, C = bq.OptionC, D = bq.OptionD } });
                    }
                }
                questionsList = questionsList.OrderBy(x => Guid.NewGuid()).ToList();
            }
            else
            {
                questionsList = quiz.Questions.Select(q => new { id = q.Id, text = q.QuestionText, image = q.QuestionImagePath, skill = q.SkillType, options = new { A = q.OptionA, B = q.OptionB, C = q.OptionC, D = q.OptionD } }).Cast<object>().ToList();
            }

            return Ok(new { success = true, quiz = new { quiz.Id, quiz.Title, quiz.DurationInMinutes, quiz.IsFinalExam }, questions = questionsList });
        }

        [HttpPost("quiz/submit")]
        public async Task<IActionResult> SubmitQuiz([FromBody] QuizSubmissionModel submission)
        {
            var quiz = await _context.Quizzes.Include(q => q.Questions).Include(q => q.Lesson).FirstOrDefaultAsync(q => q.Id == submission.QuizId);
            if (quiz == null) return NotFound(new { success = false, message = "الاختبار غير موجود" });

            int correctAnswersCount = 0;
            int totalQuestions = submission.Answers?.Count ?? 0;

            if (quiz.IsSimulator)
            {
                var answeredIds = submission.Answers?.Keys.ToList() ?? new List<int>();
                var bankQuestions = await _context.BankQuestions.Where(q => answeredIds.Contains(q.Id)).ToListAsync();
                foreach (var bq in bankQuestions) { if (submission.Answers != null && submission.Answers.ContainsKey(bq.Id) && submission.Answers[bq.Id] == bq.CorrectOption) correctAnswersCount++; }
            }
            else
            {
                totalQuestions = quiz.Questions.Count;
                foreach (var question in quiz.Questions) { if (submission.Answers != null && submission.Answers.ContainsKey(question.Id) && submission.Answers[question.Id] == question.CorrectOption) correctAnswersCount++; }
            }

            double scorePercentage = totalQuestions > 0 ? ((double)correctAnswersCount / totalQuestions) * 100 : 0;
            bool passed = scorePercentage >= quiz.MinimumPassScore;

            var scoreRecord = new StudentScore { StudentId = submission.StudentId, QuizId = submission.QuizId, ScorePercentage = Math.Round(scorePercentage, 2), CorrectAnswers = correctAnswersCount, TotalQuestions = totalQuestions, Passed = passed, DateTaken = DateTime.Now };
            _context.StudentScores.Add(scoreRecord);

            if (passed && quiz.IsFinalExam)
            {
                int courseId = quiz.CourseId ?? (quiz.Lesson?.CourseId ?? 0);
                if (courseId > 0 && !await _context.Certificates.AnyAsync(c => c.StudentId == submission.StudentId && c.CourseId == courseId))
                {
                    var allCourseScores = await _context.StudentScores.Include(s => s.Quiz).ThenInclude(q => q.Lesson)
                        .Where(s => s.StudentId == submission.StudentId && (s.Quiz.CourseId == courseId || s.Quiz.Lesson.CourseId == courseId)).ToListAsync();
                    allCourseScores.Add(scoreRecord);
                    double averageScore = allCourseScores.Any() ? allCourseScores.Average(s => s.ScorePercentage) : scorePercentage;

                    _context.Certificates.Add(new Certificate { StudentId = submission.StudentId, CourseId = courseId, SerialNumber = "ALD-" + DateTime.Now.Year + "-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper(), FinalScore = Math.Round(averageScore, 2), IssueDate = DateTime.Now });
                }
            }
            await _context.SaveChangesAsync();

            return Ok(new { success = true, score = Math.Round(scorePercentage, 2), correctCount = correctAnswersCount, total = totalQuestions, passed, isFinal = quiz.IsFinalExam });
        }

        [HttpGet("my-results/{studentId}")]
        public async Task<IActionResult> GetMyResults(int studentId)
        {
            var scores = await _context.StudentScores.Include(s => s.Quiz).ThenInclude(q => q.Course)
                .Where(s => s.StudentId == studentId).OrderByDescending(s => s.DateTaken)
                .Select(s => new { s.Id, QuizTitle = s.Quiz.Title, CourseTitle = s.Quiz.Course != null ? s.Quiz.Course.Title : s.Quiz.Lesson.Course.Title, s.ScorePercentage, s.CorrectAnswers, s.TotalQuestions, s.Passed, s.DateTaken, s.Quiz.IsFinalExam })
                .ToListAsync();

            var certificates = await _context.Certificates.Include(c => c.Course)
                .Where(c => c.StudentId == studentId).OrderByDescending(c => c.IssueDate)
                .Select(c => new { c.SerialNumber, CourseTitle = c.Course.Title, c.FinalScore, c.IssueDate })
                .ToListAsync();

            return Ok(new { success = true, scores, certificates });
        }

        [HttpGet("leaderboard/{courseId}")]
        public async Task<IActionResult> GetLeaderboard(int courseId)
        {
            var leaderboard = await _context.StudentScores.Include(s => s.Student).Include(s => s.Quiz).ThenInclude(q => q.Lesson)
                .Where(s => s.Passed && (s.Quiz.CourseId == courseId || (s.Quiz.Lesson != null && s.Quiz.Lesson.CourseId == courseId)))
                .GroupBy(s => new { s.StudentId, s.Student.FullName })
                .Select(g => new { StudentName = g.Key.FullName, AverageScore = Math.Round(g.Average(s => s.ScorePercentage), 1), TotalExamsTaken = g.Count() })
                .OrderByDescending(x => x.AverageScore).ThenByDescending(x => x.TotalExamsTaken).Take(10).ToListAsync();

            return Ok(new { success = true, leaderboard });
        }

        // ==========================================
        // 6. Forum (المنتدى والنقاشات)
        // ==========================================
        [HttpGet("forum/{courseId}")]
        public async Task<IActionResult> GetForumPosts(int courseId)
        {
            var posts = await _context.ForumPosts
                .Include(p => p.User)
                .Include(p => p.Replies).ThenInclude(r => r.User)
                .Where(p => p.CourseId == courseId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new {
                    p.Id,
                    p.Content,
                    CreatedAt = p.CreatedAt.ToString("yyyy/MM/dd hh:mm tt"),
                    UserName = p.User.FullName,
                    UserRole = p.User.Role,
                    Replies = p.Replies.OrderBy(r => r.CreatedAt).Select(r => new { r.Id, r.Content, CreatedAt = r.CreatedAt.ToString("yyyy/MM/dd hh:mm tt"), UserName = r.User.FullName, UserRole = r.User.Role })
                }).ToListAsync();

            return Ok(new { success = true, posts });
        }

        [HttpPost("forum/post")]
        public async Task<IActionResult> AddForumPost([FromBody] ForumPostRequest req)
        {
            _context.ForumPosts.Add(new ForumPost { CourseId = req.CourseId, UserId = req.UserId, Content = req.Content, CreatedAt = DateTime.Now });
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "تم إضافة سؤالك للمنتدى" });
        }

        [HttpPost("forum/reply")]
        public async Task<IActionResult> AddForumReply([FromBody] ForumReplyRequest req)
        {
            _context.ForumReplies.Add(new ForumReply { ForumPostId = req.PostId, UserId = req.UserId, Content = req.Content, CreatedAt = DateTime.Now });
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "تم إضافة الرد بنجاح" });
        }
    }

    // ==========================================
    // DTOs (Data Transfer Objects)
    // ==========================================
    public class LoginRequest { public string PhoneNumber { get; set; } public string Password { get; set; } }
    public class RegisterRequest { public string FullName { get; set; } public string PhoneNumber { get; set; } public string Password { get; set; } }
    public class UpdateProfileRequest { public int UserId { get; set; } public string FullName { get; set; } public string PhoneNumber { get; set; } public string? NewPassword { get; set; } }
    public class ForumPostRequest { public int CourseId { get; set; } public int UserId { get; set; } public string Content { get; set; } }
    public class ForumReplyRequest { public int PostId { get; set; } public int UserId { get; set; } public string Content { get; set; } }
    public class QuizSubmissionModel { public int QuizId { get; set; } public int StudentId { get; set; } public Dictionary<int, string> Answers { get; set; } }
}