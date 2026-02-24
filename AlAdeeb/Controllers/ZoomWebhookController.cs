using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using AlAdeeb.Data;
using AlAdeeb.Models;
using System.Linq;

namespace AlAdeeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ZoomWebhookController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ZoomWebhookController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("recording-completed")]
        public async Task<IActionResult> RecordingCompleted()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            using var jsonDoc = JsonDocument.Parse(body);
            var root = jsonDoc.RootElement;

            if (root.GetProperty("event").GetString() == "endpoint.url_validation")
            {
                return Ok();
            }

            if (root.GetProperty("event").GetString() == "recording.completed")
            {
                var payload = root.GetProperty("payload");
                var objectData = payload.GetProperty("object");

                var joinUrl = objectData.GetProperty("join_url").GetString();
                var recordingFiles = objectData.GetProperty("recording_files");
                string playUrl = "";

                foreach (var file in recordingFiles.EnumerateArray())
                {
                    if (file.GetProperty("file_type").GetString() == "MP4")
                    {
                        playUrl = file.GetProperty("play_url").GetString();
                        break;
                    }
                }

                var session = await _context.LiveSessions
                    .FirstOrDefaultAsync(l => l.LiveUrl.Contains(joinUrl) && !l.IsCompleted);

                if (session != null && !string.IsNullOrEmpty(playUrl))
                {
                    session.RecordedVideoUrl = playUrl;
                    session.IsCompleted = true;

                    // 1. إنشاء درس جديد بشكل آلي فور وصول الـ Webhook
                    int currentLessonCount = await _context.Lessons.CountAsync(l => l.CourseId == session.CourseId);
                    var newLesson = new Lesson
                    {
                        CourseId = session.CourseId,
                        Title = "تسجيل بث: " + session.Title,
                        OrderIndex = currentLessonCount + 1
                    };

                    _context.Lessons.Add(newLesson);
                    await _context.SaveChangesAsync();

                    // 2. إضافة التسجيل داخل هذا الدرس
                    var newMaterial = new LessonMaterial
                    {
                        LessonId = newLesson.Id,
                        Title = session.Title,
                        MaterialType = "RecordedLive",
                        UrlOrPath = playUrl,
                        OrderIndex = 1
                    };

                    _context.LessonMaterials.Add(newMaterial);

                    await _context.SaveChangesAsync();
                }
            }

            return Ok();
        }
    }
}