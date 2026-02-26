using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AlAdeeb.Models
{
    public class ApplicationUser
    {
        [Key]
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Username { get; set; }
        public string PhoneNumber { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; } // Admin, Student, Teacher
        public DateTime CreatedAt { get; set; }

        // تم إعادة هذا الحقل لحل خطأ الـ Session
        public string? CurrentSessionId { get; set; }

        // --- حماية الأجهزة ---
        public string? ActiveSessionsJson { get; set; } = "[]";
        public int AllowedDevicesCount { get; set; } = 1; // الافتراضي جهاز واحد

        public string? VerificationCode { get; set; }
        public DateTime? VerificationCodeExpiry { get; set; }
    }

    public class Course
    {
        [Key]
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }
        public bool IsActive { get; set; }

        public int? AccessDurationMonths { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // --- المعلم المسؤول عن الكورس ---
        public int? TeacherId { get; set; }
        public ApplicationUser Teacher { get; set; }

        public ICollection<Lesson> Lessons { get; set; }
        public ICollection<Quiz> Quizzes { get; set; }
    }

    public class Lesson
    {
        [Key]
        public int Id { get; set; }
        public int CourseId { get; set; }
        public Course Course { get; set; }
        public string Title { get; set; }
        public int OrderIndex { get; set; }

        public ICollection<LessonMaterial> Materials { get; set; }
        public ICollection<Quiz> Quizzes { get; set; }
    }

    public class LessonMaterial
    {
        [Key]
        public int Id { get; set; }
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; }
        public string Title { get; set; }
        public string MaterialType { get; set; }
        public string UrlOrPath { get; set; }
        public int OrderIndex { get; set; }
        public bool IsFreeSample { get; set; } = false;
    }

    public class SubscriptionRequest
    {
        [Key]
        public int Id { get; set; }
        public int StudentId { get; set; }
        public ApplicationUser Student { get; set; }
        public int CourseId { get; set; }
        public Course Course { get; set; }
        public string Status { get; set; }
        public DateTime RequestDate { get; set; }
        public string? ReceiptImagePath { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public class Quiz
    {
        [Key]
        public int Id { get; set; }
        public int? CourseId { get; set; }
        public Course Course { get; set; }
        public int? LessonId { get; set; }
        public Lesson Lesson { get; set; }
        public string Title { get; set; }
        public int DurationInMinutes { get; set; }
        public bool IsFinalExam { get; set; } = false;
        public double MinimumPassScore { get; set; } = 50.0;
        public ICollection<Question> Questions { get; set; }
    }

    public class Question
    {
        [Key]
        public int Id { get; set; }
        public int QuizId { get; set; }
        public Quiz Quiz { get; set; }

        // --- مهارة السؤال (تظهر في الجانب الأيمن من المحاكي) ---
        public string? SkillType { get; set; } // مثال: التناظر اللفظي، إكمال الجمل

        public string? QuestionText { get; set; }
        public string? QuestionImagePath { get; set; }
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
        public string CorrectOption { get; set; }
    }

    public class LiveSession { [Key] public int Id { get; set; } public int CourseId { get; set; } public Course Course { get; set; } public string Title { get; set; } public string LiveUrl { get; set; } public DateTime ScheduledDate { get; set; } public string? RecordedVideoUrl { get; set; } public bool IsCompleted { get; set; } = false; }
    public class StudentScore { [Key] public int Id { get; set; } public int StudentId { get; set; } public ApplicationUser Student { get; set; } public int QuizId { get; set; } public Quiz Quiz { get; set; } public double ScorePercentage { get; set; } public int CorrectAnswers { get; set; } public int TotalQuestions { get; set; } public bool Passed { get; set; } public DateTime DateTaken { get; set; } }
    public class Certificate { [Key] public int Id { get; set; } public int StudentId { get; set; } public ApplicationUser Student { get; set; } public int CourseId { get; set; } public Course Course { get; set; } public string SerialNumber { get; set; } public double FinalScore { get; set; } public DateTime IssueDate { get; set; } }
    public class ForumPost { [Key] public int Id { get; set; } public int CourseId { get; set; } public Course Course { get; set; } public int UserId { get; set; } public ApplicationUser User { get; set; } [Required] public string Content { get; set; } public DateTime CreatedAt { get; set; } public ICollection<ForumReply> Replies { get; set; } }
    public class ForumReply { [Key] public int Id { get; set; } public int ForumPostId { get; set; } public ForumPost ForumPost { get; set; } public int UserId { get; set; } public ApplicationUser User { get; set; } [Required] public string Content { get; set; } public DateTime CreatedAt { get; set; } }
}