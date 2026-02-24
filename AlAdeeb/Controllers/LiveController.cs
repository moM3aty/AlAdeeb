using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using AlAdeeb.Data;
using AlAdeeb.Models;

namespace AlAdeeb.Controllers
{
    public class LiveController : Controller
    {
        private readonly AppDbContext _context;

        public LiveController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> ManageLiveSessions()
        {
            var sessions = await _context.LiveSessions
                .Include(l => l.Course)
                .OrderByDescending(l => l.ScheduledDate)
                .ToListAsync();

            ViewBag.Courses = await _context.Courses.ToListAsync();
            return View(sessions);
        }

        [HttpPost]
        public async Task<IActionResult> CreateLiveSession(LiveSession model)
        {
            ModelState.Remove("Course");
            ModelState.Remove("RecordedVideoUrl");
            if (ModelState.IsValid)
            {
                
                _context.LiveSessions.Add(model);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(ManageLiveSessions));
        }

        [HttpPost]
        public async Task<IActionResult> UploadRecording(int sessionId, string recordedUrl)
        {
            var session = await _context.LiveSessions.FindAsync(sessionId);
            if (session != null && !session.IsCompleted)
            {
                session.RecordedVideoUrl = recordedUrl;
                session.IsCompleted = true;

                // 1. إنشاء درس جديد داخل الكورس تلقائياً
                int currentLessonCount = await _context.Lessons.CountAsync(l => l.CourseId == session.CourseId);
                var newLesson = new Lesson
                {
                    CourseId = session.CourseId,
                    Title = "تسجيل بث: " + session.Title,
                    OrderIndex = currentLessonCount + 1
                };

                _context.Lessons.Add(newLesson);
                await _context.SaveChangesAsync(); // الحفظ للحصول على معرّف الدرس الجديد (Id)

                // 2. إضافة التسجيل كمحتوى (فيديو) داخل هذا الدرس
                var newMaterial = new LessonMaterial
                {
                    LessonId = newLesson.Id,
                    Title = session.Title,
                    MaterialType = "RecordedLive", // لتشغيله في المشغل المخصص
                    UrlOrPath = recordedUrl,
                    OrderIndex = 1
                };

                _context.LessonMaterials.Add(newMaterial);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(ManageLiveSessions));
        }
    }
}