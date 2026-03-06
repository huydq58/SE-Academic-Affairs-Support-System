using Microsoft.AspNetCore.Mvc;

namespace SE_Academic_Affairs_Support_System.Controllers
{
    public class LoginController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        public IActionResult Login(string Username, string Password)
        {
            if (Username == "admin" && Password == "123")
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Sai tài khoản hoặc mật khẩu";
            return View();
        }
    }
}
