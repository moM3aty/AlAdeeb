using Microsoft.AspNetCore.Mvc;

namespace AlAdeeb.Controllers
{
    public class ForumController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
