using Microsoft.EntityFrameworkCore;
using AlAdeeb.Models;

namespace AlAdeeb.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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
        public DbSet<ForumPost> ForumPosts { get; set; }
        public DbSet<ForumReply> ForumReplies { get; set; }

        // جداول بنك الأسئلة
        public DbSet<QuestionBankSection> QuestionBankSections { get; set; }
        public DbSet<BankQuestion> BankQuestions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SubscriptionRequest>().HasOne(s => s.Student).WithMany().HasForeignKey(s => s.StudentId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<SubscriptionRequest>().HasOne(s => s.Course).WithMany().HasForeignKey(s => s.CourseId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Lesson>().HasOne(l => l.Course).WithMany(c => c.Lessons).HasForeignKey(l => l.CourseId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<LessonMaterial>().HasOne(m => m.Lesson).WithMany(l => l.Materials).HasForeignKey(m => m.LessonId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Quiz>().HasOne(q => q.Lesson).WithMany(l => l.Quizzes).HasForeignKey(q => q.LessonId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Certificate>().HasOne(c => c.Student).WithMany().HasForeignKey(c => c.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ForumPost>().HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ForumReply>().HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ForumReply>().HasOne(r => r.ForumPost).WithMany(p => p.Replies).HasForeignKey(r => r.ForumPostId).OnDelete(DeleteBehavior.Cascade);

            // علاقات بنك الأسئلة
            modelBuilder.Entity<QuestionBankSection>()
                .HasOne(s => s.Course)
                .WithMany(c => c.QuestionBankSections)
                .HasForeignKey(s => s.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BankQuestion>()
                .HasOne(q => q.Section)
                .WithMany(s => s.Questions)
                .HasForeignKey(q => q.QuestionBankSectionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}