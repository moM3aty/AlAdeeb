using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using AlAdeeb.Data;

namespace AlAdeeb.Controllers
{
    // هذا الكنترولر يستقبل الطلبات من سيرفرات Zoom تلقائياً
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
            // 1. قراءة البيانات القادمة من زووم
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            using var jsonDoc = JsonDocument.Parse(body);
            var root = jsonDoc.RootElement;

            // التحقق من نوع الحدث
            if (root.GetProperty("event").GetString() == "endpoint.url_validation")
            {
                // هذا الجزء خاص بتأكيد حساب زووم للمرة الأولى
                return Ok();
            }

            if (root.GetProperty("event").GetString() == "recording.completed")
            {
                var payload = root.GetProperty("payload");
                var objectData = payload.GetProperty("object");

                // استخراج رابط الاجتماع (للبحث عنه في قاعدة البيانات)
                var joinUrl = objectData.GetProperty("join_url").GetString();

                // استخراج روابط التسجيل
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

                // 2. تحديث قاعدة البيانات تلقائياً
                var session = await _context.LiveSessions
                    .FirstOrDefaultAsync(l => l.LiveUrl.Contains(joinUrl) && !l.IsCompleted);

                if (session != null && !string.IsNullOrEmpty(playUrl))
                {
                    session.RecordedVideoUrl = playUrl;
                    session.IsCompleted = true;
                    await _context.SaveChangesAsync();
                }
            }

            return Ok();
        }
    }
}