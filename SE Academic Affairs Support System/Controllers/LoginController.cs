using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.Controllers
{
    public class LoginController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<LoginController> _logger;

        public LoginController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ILogger<LoginController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(string Username, string Password, bool RememberMe = false)
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ tài khoản và mật khẩu.";
                return View();
            }

            var user = await _userManager.FindByNameAsync(Username);


            if (user == null)
            {
                ViewBag.Error = "Sai tài khoản hoặc mật khẩu.";
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(
                user,
                Password,
                RememberMe,
                lockoutOnFailure: true
            );

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"Chào mừng {user.FullName ?? user.UserName} đã đăng nhập thành công!";
                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                ViewBag.Error = "Tài khoản bị tạm khóa do đăng nhập sai quá nhiều lần. Vui lòng thử lại sau.";
                return View();
            }

            if (result.IsNotAllowed)
            {
                ViewBag.Error = "Tài khoản chưa được xác nhận email. Liên hệ admin.";
                return View();
            }

            ViewBag.Error = "Sai tài khoản hoặc mật khẩu.";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}