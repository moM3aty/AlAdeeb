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
    [Authorize(Roles = "Student")]
    public class QuizController : ControllerBase
    {
        private readonly AppDbContext _context;

        public QuizController(AppDbContext context)
        {
            _context = context;
        }

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
            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                .Include(q => q.Lesson)
                .FirstOrDefaultAsync(q => q.Id == submission.QuizId);

            if (quiz == null)
                return NotFound("الاختبار غير موجود");

            int correctAnswersCount = 0;
            int totalQuestions = submission.TotalQuestions;

            if (quiz.IsSimulator)
            {
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

            double scorePercentage = totalQuestions > 0 ? ((double)correctAnswersCount / totalQuestions) * 100 : 0;
            bool passed = scorePercentage >= quiz.MinimumPassScore;

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
            // التعديل: إصدار الشهادة بمتوسط درجات جميع الاختبارات بالكورس
            // =========================================================
            if (passed && quiz.IsFinalExam)
            {
                int courseId = quiz.CourseId ?? (quiz.Lesson != null ? quiz.Lesson.CourseId : 0);

                if (courseId > 0)
                {
                    bool hasCert = await _context.Certificates
                        .AnyAsync(c => c.StudentId == submission.StudentId && c.CourseId == courseId);

                    if (!hasCert)
                    {
                        // جلب جميع درجات الطالب السابقة في هذا الكورس بالتحديد
                        var allCourseScores = await _context.StudentScores
                            .Include(s => s.Quiz)
                            .ThenInclude(q => q.Lesson)
                            .Where(s => s.StudentId == submission.StudentId &&
                                        (s.Quiz.CourseId == courseId || s.Quiz.Lesson.CourseId == courseId))
                            .ToListAsync();

                        // إضافة نتيجة الاختبار النهائي الحالي للقائمة
                        allCourseScores.Add(scoreRecord);

                        // حساب المتوسط لجميع الاختبارات (الدروس + النهائي)
                        double averageScore = allCourseScores.Any() ? allCourseScores.Average(s => s.ScorePercentage) : scorePercentage;

                        var cert = new Certificate
                        {
                            StudentId = submission.StudentId,
                            CourseId = courseId,
                            SerialNumber = "ALD-" + DateTime.Now.Year + "-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
                            FinalScore = Math.Round(averageScore, 2), // وضع المتوسط النهائي في الشهادة
                            IssueDate = DateTime.Now
                        };
                        _context.Certificates.Add(cert);
                    }
                }
            }

            await _context.SaveChangesAsync();

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