using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlAdeeb.Models
{
    // 1. المستخدمين
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

    // 2. الكورس الأساسي
    public class Course
    {
        [Key]
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; } = true;

        // العلاقات الجديدة
        public ICollection<Lesson> Lessons { get; set; } // الكورس يحتوي على دروس
        public ICollection<Quiz> Quizzes { get; set; } // قد يحتوي على اختبار نهائي
    }

    // 3. الدرس (الكيان الجديد الذي يجمع الملفات والاختبارات)
    public class Lesson
    {
        [Key]
        public int Id { get; set; }
        public int CourseId { get; set; }
        public Course Course { get; set; }
        public string Title { get; set; }
        public int OrderIndex { get; set; } // ترتيب الدرس داخل الكورس

        // محتويات الدرس
        public ICollection<LessonMaterial> Materials { get; set; } // فيديوهات و PDF
        public ICollection<Quiz> Quizzes { get; set; } // اختبارات الدرس
    }

    // 4. محتوى الدرس (فيديو أو PDF)
    public class LessonMaterial
    {
        [Key]
        public int Id { get; set; }
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; }
        public string Title { get; set; }
        public string MaterialType { get; set; } // "YouTube", "PDF"
        public string UrlOrPath { get; set; }
        public int OrderIndex { get; set; }
    }

    // 5. الاختبارات (تم التعديل ليدعم اختبار للدرس أو اختبار نهائي للكورس)
    public class Quiz
    {
        [Key]
        public int Id { get; set; }

        // قد يكون تابع لدرس معين
        public int? LessonId { get; set; }
        public Lesson Lesson { get; set; }

        // أو قد يكون تابع للكورس ككل (اختبار نهائي)
        public int? CourseId { get; set; }
        public Course Course { get; set; }

        public string Title { get; set; }
        public bool IsFinalExam { get; set; } = false; // تحديد إذا كان هذا هو الاختبار النهائي
        public int DurationInMinutes { get; set; }
        public decimal MinimumPassScore { get; set; } = 50.0m; // درجة النجاح المطلوبة للشهادة

        public ICollection<Question> Questions { get; set; }
    }

    // 6. الأسئلة
    public class Question
    {
        [Key]
        public int Id { get; set; }
        public int QuizId { get; set; }
        public Quiz Quiz { get; set; }
        public string QuestionText { get; set; }
        public string? QuestionImagePath { get; set; }
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
        public string CorrectOption { get; set; }
    }

    // 7. نتيجة الطالب
    public class StudentScore
    {
        [Key]
        public int Id { get; set; }
        public int StudentId { get; set; }
        public ApplicationUser Student { get; set; }
        public int QuizId { get; set; }
        public Quiz Quiz { get; set; }
        public double ScorePercentage { get; set; }
        public int CorrectAnswers { get; set; }
        public int TotalQuestions { get; set; }
        public bool Passed { get; set; } // هل اجتاز الاختبار؟
        public DateTime DateTaken { get; set; } = DateTime.Now;
    }

    // 8. شهادات التقدير (الكيان الجديد)
    public class Certificate
    {
        [Key]
        public int Id { get; set; }
        public int StudentId { get; set; }
        public ApplicationUser Student { get; set; }
        public int CourseId { get; set; }
        public Course Course { get; set; }
        public string SerialNumber { get; set; } // رقم تسلسلي فريد للشهادة
        public double FinalScore { get; set; } // درجة الاختبار النهائي
        public DateTime IssueDate { get; set; } = DateTime.Now;
    }

    // 9. طلبات الاشتراك
    public class SubscriptionRequest
    {
        [Key]
        public int Id { get; set; }
        public int StudentId { get; set; }
        public ApplicationUser Student { get; set; }
        public int CourseId { get; set; }
        public Course Course { get; set; }
        public string ReceiptImagePath { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime RequestDate { get; set; } = DateTime.Now;
    }

    // 10. البثوث المباشرة
    public class LiveSession
    {
        [Key]
        public int Id { get; set; }
        public int CourseId { get; set; }
        public Course Course { get; set; }
        [Required(ErrorMessage = "عنوان البث مطلوب")]
        public string Title { get; set; }
        [Required(ErrorMessage = "رابط البث مطلوب")]
        public string LiveUrl { get; set; }
        [Required(ErrorMessage = "موعد البث مطلوب")]
        public DateTime ScheduledDate { get; set; }
        public string? RecordedVideoUrl { get; set; }
        public bool IsCompleted { get; set; } = false;
    }
}