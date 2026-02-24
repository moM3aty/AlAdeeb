using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AlAdeeb.Data;
using System.Threading.Tasks;

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

            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}