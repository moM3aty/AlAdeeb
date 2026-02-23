using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AlAdeeb.Models
{
    // 1. نموذج المستخدم (طالب أو أدمن)
    public class ApplicationUser
    {
        [Key]
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; } // "Student" or "Admin"
        public string PhoneNumber { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    // 2. نموذج الكورس / الدورة
    public class Course
    {
        [Key]
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }

        public string? ImageUrl { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<CourseMaterial> Materials { get; set; }
        public ICollection<Quiz> Quizzes { get; set; }
    }

    // 3. نموذج طلبات الاشتراك (لرفع الفاتورة والقبول/الرفض)
    public class SubscriptionRequest
    {
        [Key]
        public int Id { get; set; }
        public int StudentId { get; set; }
        public ApplicationUser Student { get; set; }
        public int CourseId { get; set; }
        public Course Course { get; set; }
        public string ReceiptImagePath { get; set; } // مسار صورة الإيصال
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
        public DateTime RequestDate { get; set; } = DateTime.Now;
    }

    // 4. نموذج المحتوى التعليمي (فيديو يوتيوب، PDF، بث مسجل)
    public class CourseMaterial
    {
        [Key]
        public int Id { get; set; }
        public int CourseId { get; set; }
        public Course Course { get; set; }
        public string Title { get; set; }
        public string MaterialType { get; set; } // "YouTube", "PDF", "RecordedLive"
        public string UrlOrPath { get; set; }
        public int OrderIndex { get; set; }
    }

    // 5. نموذج الاختبارات
    public class Quiz
    {
        [Key]
        public int Id { get; set; }
        public int CourseId { get; set; }
        public Course Course { get; set; }
        public string Title { get; set; }
        public int DurationInMinutes { get; set; } // وقت الاختبار للتايمر
        public ICollection<Question> Questions { get; set; }
    }
    public class LiveSession
    {
        [Key]
        public int Id { get; set; }

        public int CourseId { get; set; }
        public Course Course { get; set; }

        [Required(ErrorMessage = "عنوان البث مطلوب")]
        public string Title { get; set; }

        [Required(ErrorMessage = "رابط البث مطلوب (Zoom / Meet)")]
        public string LiveUrl { get; set; }

        [Required(ErrorMessage = "موعد البث مطلوب")]
        public DateTime ScheduledDate { get; set; }

        public string RecordedVideoUrl { get; set; } // رابط التسجيل بعد انتهاء البث المباشر

        public bool IsCompleted { get; set; } = false; // هل انتهى البث؟
    }
    public class StudentScore
    {
        [Key]
        public int Id { get; set; }

        public int StudentId { get; set; }
        public ApplicationUser Student { get; set; }

        public int QuizId { get; set; }
        public Quiz Quiz { get; set; }

        public double ScorePercentage { get; set; } // النسبة المئوية (مثلاً 85.5)
        public int CorrectAnswers { get; set; }
        public int TotalQuestions { get; set; }

        public DateTime DateTaken { get; set; } = DateTime.Now;
    }
    // 6. نموذج الأسئلة (يدعم النصوص والصور)
    public class Question
    {
        [Key]
        public int Id { get; set; }
        public int QuizId { get; set; }
        public Quiz Quiz { get; set; }
        public string QuestionText { get; set; }
        public string? QuestionImagePath { get; set; } // في حال كان السؤال صورة
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
        public string CorrectOption { get; set; } // "A", "B", "C", or "D"
    }
}