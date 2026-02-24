using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using AlAdeeb.Data;
using AlAdeeb.Models;
using System;

namespace AlAdeeb.Controllers
{
    [Authorize]
    public class CourseController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public CourseController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int id)
        {
            var course = await _context.Courses
                .Include(c => c.Lessons)
                    .ThenInclude(l => l.Materials)
                .Include(c => c.Lessons)
                    .ThenInclude(l => l.Quizzes)
                .Include(c => c.Quizzes) // جلب الاختبارات النهائية التابعة للكورس مباشرة
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return NotFound();

            // تم التعديل هنا ليعود للمسار الافتراضي Views/Course/Details.cshtml
            return View(course);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddLesson(int courseId, string title)
        {
            var lesson = new Lesson
            {
                CourseId = courseId,
                Title = title,
                OrderIndex = _context.Lessons.Count(l => l.CourseId == courseId) + 1
            };
            _context.Lessons.Add(lesson);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة الوحدة/الدرس بنجاح.";
            return RedirectToAction("Details", new { id = courseId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddMaterial(int lessonId, int courseId, string title, string materialType, string youtubeUrl, IFormFile pdfFile)
        {
            var material = new LessonMaterial
            {
                LessonId = lessonId,
                Title = title,
                MaterialType = materialType,
                OrderIndex = _context.LessonMaterials.Count(m => m.LessonId == lessonId) + 1
            };

            if (materialType == "YouTube" || materialType == "RecordedLive")
            {
                material.UrlOrPath = youtubeUrl;
            }
            else if (materialType == "PDF" && pdfFile != null)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads/pdfs");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + pdfFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await pdfFile.CopyToAsync(fileStream);
                }

                material.UrlOrPath = "/uploads/pdfs/" + uniqueFileName;
            }

            _context.LessonMaterials.Add(material);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة المحتوى بنجاح.";
            return RedirectToAction("Details", new { id = courseId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteLesson(int id, int courseId)
        {
            var lesson = await _context.Lessons.FindAsync(id);
            if (lesson != null)
            {
                _context.Lessons.Remove(lesson);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف الدرس بكافة محتوياته.";
            }
            return RedirectToAction("Details", new { id = courseId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMaterial(int id, int courseId)
        {
            var material = await _context.LessonMaterials.FindAsync(id);
            if (material != null)
            {
                _context.LessonMaterials.Remove(material);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف الملف بنجاح.";
            }
            return RedirectToAction("Details", new { id = courseId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteQuiz(int id, int courseId)
        {
            var quiz = await _context.Quizzes.FindAsync(id);
            if (quiz != null)
            {
                _context.Quizzes.Remove(quiz);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف الاختبار بنجاح.";
            }
            return RedirectToAction("Details", new { id = courseId });
        }
    }
}