using Microsoft.AspNetCore.Mvc;
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
    // [Authorize(Roles = "Admin")]
    public class CourseController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public CourseController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // 1. عرض تفاصيل الكورس وإضافة محتوى له (فيديو / PDF)
        public async Task<IActionResult> Details(int id)
        {
            var course = await _context.Courses
                .Include(c => c.Materials)
                .Include(c => c.Quizzes)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return NotFound();

            return View(course); // المسار: Views/Admin/CourseDetails.cshtml
        }

        // 2. رفع محتوى جديد (فيديو يوتيوب أو ملف PDF)
        [HttpPost]
        public async Task<IActionResult> AddMaterial(int courseId, string title, string materialType, string youtubeUrl, IFormFile pdfFile)
        {
            var material = new CourseMaterial
            {
                CourseId = courseId,
                Title = title,
                MaterialType = materialType,
                OrderIndex = _context.CourseMaterials.Count(m => m.CourseId == courseId) + 1
            };

            if (materialType == "YouTube" || materialType == "RecordedLive")
            {
                material.UrlOrPath = youtubeUrl; // تخزين رابط اليوتيوب
            }
            else if (materialType == "PDF" && pdfFile != null)
            {
                // مسار حفظ ملفات الـ PDF
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

            _context.CourseMaterials.Add(material);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = courseId });
        }
    }
}