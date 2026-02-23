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

        // 1. الصفحة الرئيسية (الواجهة التسويقية)
        public async Task<IActionResult> Index()
        {
            // جلب بعض الإحصائيات لعرضها في الصفحة الرئيسية لزيادة الثقة
            ViewBag.CoursesCount = await _context.Courses.CountAsync(c => c.IsActive);
            ViewBag.StudentsCount = await _context.Users.CountAsync(u => u.Role == "Student");

            // إضافة رقم وهمي في البداية ليعطي انطباعاً بالقوة، ويتم جمعه مع المسجلين الفعليين
            ViewBag.TotalStudentsDisplay = 10000 + ViewBag.StudentsCount;

            return View();
        }

        // 2. صفحة من نحن (عن المدرب)
        public IActionResult About()
        {
            return View();
        }

        // 3. صفحة سياسة الخصوصية والشروط
        public IActionResult Privacy()
        {
            return View();
        }

        // 4. صفحة الخطأ
        public IActionResult Error()
        {
            return View();
        }
    }
}