using Microsoft.EntityFrameworkCore;
using AlAdeeb.Models;

namespace AlAdeeb.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // تسجيل الجداول
        public DbSet<ApplicationUser> Users { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<LessonMaterial> LessonMaterials { get; set; }
        public DbSet<SubscriptionRequest> SubscriptionRequests { get; set; }
        public DbSet<Quiz> Quizzes { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<LiveSession> LiveSessions { get; set; }
        public DbSet<StudentScore> StudentScores { get; set; }
        public DbSet<Certificate> Certificates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // علاقات طلبات الاشتراك
            modelBuilder.Entity<SubscriptionRequest>()
                .HasOne(s => s.Student)
                .WithMany()
                .HasForeignKey(s => s.StudentId)
                .OnDelete(DeleteBehavior.Restrict); // منع حذف الطالب إذا كان له اشتراك

            modelBuilder.Entity<SubscriptionRequest>()
                .HasOne(s => s.Course)
                .WithMany()
                .HasForeignKey(s => s.CourseId)
                .OnDelete(DeleteBehavior.Cascade); // حذف الاشتراكات إذا تم حذف الكورس

            // علاقة الدروس بالكورس
            modelBuilder.Entity<Lesson>()
                .HasOne(l => l.Course)
                .WithMany(c => c.Lessons)
                .HasForeignKey(l => l.CourseId)
                .OnDelete(DeleteBehavior.Cascade); // حذف الدروس تلقائياً عند حذف الكورس

            // علاقة محتوى الدرس بالدرس
            modelBuilder.Entity<LessonMaterial>()
                .HasOne(m => m.Lesson)
                .WithMany(l => l.Materials)
                .HasForeignKey(m => m.LessonId)
                .OnDelete(DeleteBehavior.Cascade); // حذف الفيديوهات والملفات عند حذف الدرس

            // علاقة الاختبار بالدرس
            modelBuilder.Entity<Quiz>()
                .HasOne(q => q.Lesson)
                .WithMany(l => l.Quizzes)
                .HasForeignKey(q => q.LessonId)
                .OnDelete(DeleteBehavior.Cascade); // حذف الاختبارات المتعلقة بالدرس عند حذفه

            // علاقة الشهادات
            modelBuilder.Entity<Certificate>()
                .HasOne(c => c.Student)
                .WithMany()
                .HasForeignKey(c => c.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}