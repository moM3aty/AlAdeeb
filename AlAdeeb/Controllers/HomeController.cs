using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using AlAdeeb.Models;
using AlAdeeb.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace AlAdeeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.CoursesCount = await _context.Courses.CountAsync(c => c.IsActive);
            ViewBag.StudentsCount = await _context.Users.CountAsync(u => u.Role == "Student");
            ViewBag.TotalStudentsDisplay = 10000 + ViewBag.StudentsCount;

            var settings = await _context.PlatformSettings.FirstOrDefaultAsync();
            ViewBag.PromoVideoUrl = settings?.PromoVideoUrl;
            ViewBag.PlacementTestQuizId = settings?.PlacementTestQuizId;

            return View();
        }

        public async Task<IActionResult> About()
        {
            // جلب السيرة الذاتية من الإعدادات
            var settings = await _context.PlatformSettings.FirstOrDefaultAsync();
            ViewBag.TrainerBio = settings?.TrainerBio;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}