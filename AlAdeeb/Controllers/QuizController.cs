using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
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

            double scorePercentage = totalQuestions > 0 ? ((double)correctAnswersCount / totalQuestions) * 100 : 0;
            bool passed = scorePercentage >= (double)quiz.MinimumPassScore;

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

            string certificateSerial = null;

            // توليد شهادة اجتياز إذا كان الاختبار هو النهائي ونجح الطالب
            if (quiz.IsFinalExam && passed && quiz.CourseId.HasValue)
            {
                var existingCert = await _context.Certificates
                    .FirstOrDefaultAsync(c => c.StudentId == submission.StudentId && c.CourseId == quiz.CourseId.Value);

                if (existingCert == null)
                {
                    var cert = new Certificate
                    {
                        StudentId = submission.StudentId,
                        CourseId = quiz.CourseId.Value,
                        SerialNumber = "ALAD-" + DateTime.Now.ToString("yyyyMM") + "-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
                        FinalScore = Math.Round(scorePercentage, 2),
                        IssueDate = DateTime.Now
                    };
                    _context.Certificates.Add(cert);
                    certificateSerial = cert.SerialNumber;
                }
                else
                {
                    certificateSerial = existingCert.SerialNumber;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Score = Math.Round(scorePercentage, 2),
                CorrectCount = correctAnswersCount,
                Total = totalQuestions,
                Passed = passed,
                IsFinal = quiz.IsFinalExam,
                CertificateSerial = certificateSerial
            });
        }
    }
}