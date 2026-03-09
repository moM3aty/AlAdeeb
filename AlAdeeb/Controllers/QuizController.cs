using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using AlAdeeb.Data;
using AlAdeeb.Models;

namespace AlAdeeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Student")] // تأمين الكنترولر ليقبل فقط طلبات الطلاب المسجلين
    public class QuizController : ControllerBase
    {
        private readonly AppDbContext _context;

        public QuizController(AppDbContext context)
        {
            _context = context;
        }

        // نموذج استلام بيانات الاختبار من واجهة الطالب (AJAX)
        public class QuizSubmissionModel
        {
            public int QuizId { get; set; }
            public int StudentId { get; set; }
            public int TotalQuestions { get; set; }
            public Dictionary<int, string> Answers { get; set; }
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitQuiz([FromBody] QuizSubmissionModel submission)
        {
            // جلب الاختبار من قاعدة البيانات للتحقق من نوعه والأسئلة المرتبطة به
            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                .Include(q => q.Lesson)
                .FirstOrDefaultAsync(q => q.Id == submission.QuizId);

            if (quiz == null)
                return NotFound("الاختبار غير موجود");

            int correctAnswersCount = 0;
            int totalQuestions = submission.TotalQuestions;

            // =========================================================
            // 1. التصحيح الذكي (حسب نوع الاختبار: محاكي عشوائي أو عادي)
            // =========================================================
            if (quiz.IsSimulator)
            {
                // إذا كان محاكي عشوائي: جلب الأسئلة التي أجاب عليها الطالب من بنك الأسئلة
                var answeredIds = submission.Answers?.Keys.ToList() ?? new List<int>();
                var bankQuestions = await _context.BankQuestions
                    .Where(q => answeredIds.Contains(q.Id))
                    .ToListAsync();

                foreach (var bq in bankQuestions)
                {
                    if (submission.Answers != null && submission.Answers.ContainsKey(bq.Id))
                    {
                        if (submission.Answers[bq.Id] == bq.CorrectOption)
                            correctAnswersCount++;
                    }
                }
            }
            else
            {
                // إذا كان اختبار عادي: جلب الأسئلة الثابتة المرتبطة بالاختبار مباشرة
                totalQuestions = quiz.Questions.Count;
                foreach (var question in quiz.Questions)
                {
                    if (submission.Answers != null && submission.Answers.ContainsKey(question.Id))
                    {
                        if (submission.Answers[question.Id] == question.CorrectOption)
                            correctAnswersCount++;
                    }
                }
            }

            // حساب النسبة المئوية ومعرفة حالة الاجتياز
            double scorePercentage = totalQuestions > 0 ? ((double)correctAnswersCount / totalQuestions) * 100 : 0;
            bool passed = scorePercentage >= quiz.MinimumPassScore;

            // =========================================================
            // 2. توثيق نتيجة الطالب في قاعدة البيانات
            // =========================================================
            var scoreRecord = new StudentScore
            {
                StudentId = submission.StudentId,
                QuizId = submission.QuizId,
                ScorePercentage = Math.Round(scorePercentage, 2),
                CorrectAnswers = correctAnswersCount,
                TotalQuestions = totalQuestions,
                Passed = passed,
                DateTaken = DateTime.Now
            };

            _context.StudentScores.Add(scoreRecord);

            // =========================================================
            // 3. إصدار الشهادة تلقائياً (إذا كان اختباراً نهائياً ونجح فيه)
            // =========================================================
            if (passed && quiz.IsFinalExam)
            {
                int courseId = quiz.CourseId ?? (quiz.Lesson != null ? quiz.Lesson.CourseId : 0);

                if (courseId > 0)
                {
                    // التأكد من عدم وجود شهادة مسبقة لنفس الطالب في هذا الكورس
                    bool hasCert = await _context.Certificates
                        .AnyAsync(c => c.StudentId == submission.StudentId && c.CourseId == courseId);

                    if (!hasCert)
                    {
                        var cert = new Certificate
                        {
                            StudentId = submission.StudentId,
                            CourseId = courseId,
                            SerialNumber = "ALD-" + DateTime.Now.Year + "-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
                            FinalScore = Math.Round(scorePercentage, 2),
                            IssueDate = DateTime.Now
                        };
                        _context.Certificates.Add(cert);
                    }
                }
            }

            // حفظ جميع التغييرات (النتيجة + الشهادة إن وجدت) في قاعدة البيانات
            await _context.SaveChangesAsync();

            // إرجاع النتيجة لواجهة الطالب ليتم عرضها في المحاكي فوراً
            return Ok(new
            {
                Score = Math.Round(scorePercentage, 2),
                CorrectCount = correctAnswersCount,
                Total = totalQuestions,
                Passed = passed,
                IsFinal = quiz.IsFinalExam
            });
        }
    }
}