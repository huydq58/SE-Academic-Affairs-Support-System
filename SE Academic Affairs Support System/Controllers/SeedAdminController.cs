using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.Controllers
{
    // Chỉ dùng trong môi trường Development — xóa hoặc comment lại sau khi tạo xong admin
    //[Authorize(Roles = "Admin")]
    public class SeedAdminController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IWebHostEnvironment _env;
        private readonly AppDbContext _context;

        public SeedAdminController(
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            IWebHostEnvironment env,
            AppDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _env = env;
            _context = context;
        }

        // GET: /SeedAdmin
        [HttpGet]
        public IActionResult Index()
        {
            // Chặn truy cập ngoài môi trường Development
            if (!_env.IsDevelopment())
                return NotFound();

            return View();
        }

        // POST: /SeedAdmin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string username, string password, string fullname, string role)
        {
            if (!_env.IsDevelopment())
                return NotFound();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.";
                return View("Index");
            }

            // Tạo role nếu chưa có
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole(role));

            var existing = await _userManager.FindByNameAsync(username);
            if (existing != null)
            {
                ViewBag.Error = $"Tài khoản \"{username}\" đã tồn tại.";
                return View("Index");
            }

            var user = new User
            {
                UserName = username,
                FullName = fullname,
                Role = role,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                ViewBag.Error = string.Join("<br/>", result.Errors.Select(e => e.Description));
                return View("Index");
            }

            await _userManager.AddToRoleAsync(user, role);

            // 🔥 TẠO PROFILE TƯƠNG ỨNG
            if (role == "Student")
            {
                var student = new StudentProfile
                {
                    UserId = user.Id,
                    StudentCode = "SV" + DateTime.Now.Ticks.ToString().Substring(10) // tạm
                };

                _context.StudentProfiles.Add(student);
            }
            else if (role == "Lecturer")
            {
                var lecturer = new LecturerProfile
                {
                    UserId = user.Id,
                    LecturerCode = "GV" + DateTime.Now.Ticks.ToString().Substring(10), // tạm
                    MaxStudents = 10
                };

                _context.LecturerProfiles.Add(lecturer);
            }

            await _context.SaveChangesAsync();

            ViewBag.Success = $"Tạo tài khoản \"{username}\" với role \"{role}\" thành công!";
            return View("Index");
        }
    }
}
