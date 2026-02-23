using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using AlAdeeb.Data;
using AlAdeeb.Models;
using System;

namespace AlAdeeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
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
            public Dictionary<int, string> Answers { get; set; }
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitQuiz([FromBody] QuizSubmissionModel submission)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.Id == submission.QuizId);

            if (quiz == null) return NotFound("الاختبار غير موجود");

            int correctAnswersCount = 0;
            int totalQuestions = quiz.Questions.Count;

            // حساب الإجابات الصحيحة
            foreach (var question in quiz.Questions)
            {
                if (submission.Answers != null && submission.Answers.ContainsKey(question.Id))
                {
                    string studentAnswer = submission.Answers[question.Id];
                    if (studentAnswer == question.CorrectOption)
                    {
                        correctAnswersCount++;
                    }
                }
            }

            // حساب النسبة المئوية
            double scorePercentage = totalQuestions > 0 ? ((double)correctAnswersCount / totalQuestions) * 100 : 0;

            // --- التعديل الجوهري: حفظ النتيجة في قاعدة البيانات ---
            var scoreRecord = new StudentScore
            {
                StudentId = submission.StudentId,
                QuizId = submission.QuizId,
                ScorePercentage = Math.Round(scorePercentage, 2),
                CorrectAnswers = correctAnswersCount,
                TotalQuestions = totalQuestions,
                DateTaken = DateTime.Now
            };

            _context.Set<StudentScore>().Add(scoreRecord);
            await _context.SaveChangesAsync();
            // ----------------------------------------------------

            return Ok(new
            {
                Score = Math.Round(scorePercentage, 2),
                CorrectCount = correctAnswersCount,
                Total = totalQuestions
            });
        }
    }
}