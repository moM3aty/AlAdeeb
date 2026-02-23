using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using AlAdeeb.Data;
using AlAdeeb.Models;

namespace AlAdeeb.Controllers
{
    // [Authorize(Roles = "Admin")]
    public class LiveController : Controller
    {
        private readonly AppDbContext _context;

        public LiveController(AppDbContext context)
        {
            _context = context;
        }

        // صفحة إدارة البثوث المباشرة
        public async Task<IActionResult> ManageLiveSessions()
        {
            var sessions = await _context.LiveSessions
                .Include(l => l.Course)
                .OrderByDescending(l => l.ScheduledDate)
                .ToListAsync();

            ViewBag.Courses = await _context.Courses.ToListAsync();
            return View(sessions);
        }

        // إضافة بث مباشر جديد (Zoom / Meet)
        [HttpPost]
        public async Task<IActionResult> CreateLiveSession(LiveSession model)
        {
            if (ModelState.IsValid)
            {
                _context.LiveSessions.Add(model);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(ManageLiveSessions));
        }

        // رفع التسجيل بعد انتهاء البث
        [HttpPost]
        public async Task<IActionResult> UploadRecording(int sessionId, string recordedUrl)
        {
            var session = await _context.LiveSessions.FindAsync(sessionId);
            if (session != null)
            {
                session.RecordedVideoUrl = recordedUrl;
                session.IsCompleted = true; // تم إنهاء البث وتوفير التسجيل
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(ManageLiveSessions));
        }
    }
}