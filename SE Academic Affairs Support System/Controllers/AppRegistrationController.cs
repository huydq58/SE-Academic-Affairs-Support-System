using System.Security.Claims; 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.Services;
using SE_Academic_Affairs_Support_System.Services.AppRegistration;
using SE_Academic_Affairs_Support_System.Services.Email;


namespace SE_Academic_Affairs_Support_System.Controllers
{
    public class AppRegistrationController : Controller
    {
        private readonly IAppRegistrationService _service;
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IConfiguration _config;
        private readonly EmailService _emailService;
        public AppRegistrationController(IAppRegistrationService service, AppDbContext context, UserManager<User> userManager)
        {
            _emailService = new EmailService(_config);

            _service = service;
            _context = context;
            _userManager = userManager;
        }

        // GET: /AppRegistration
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // GET: /AppRegistration/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View(new AppRegistrationRequest());
        }

        //// POST: /AppRegistration/Submit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(AppRegistrationRequest model)
        {
            // Gán RequestId trước khi validate để tránh lỗi required trên field này
            if (string.IsNullOrEmpty(model.RequestId))
            {
                model.RequestId = Guid.NewGuid().ToString();
                ModelState.Remove(nameof(model.RequestId));
            }

            if (!ModelState.IsValid)
            {
                return View("Create", model);
            }

            await _service.CreateRequestAsync(model);

            TempData["SuccessMessage"] = $"Yêu cầu \"{model.AppName}\" đã được gửi thành công! Mã yêu cầu: {model.RequestId}";
            return RedirectToAction("Create");
        }

        //// GET: /AppRegistration/Detail/{id}
        //[HttpGet]
        //public async Task<IActionResult> Detail(string id)
        //{
        //    if (string.IsNullOrEmpty(id))
        //        return NotFound();

        //    var request = await _service.GetByIdAsync(id);
        //    if (request == null)
        //        return NotFound();

        //    return View(request);
        //}

        //// GET: /AppRegistration/List
        //[HttpGet]
        //public async Task<IActionResult> List()
        //{
        //    var requests = await _service.GetAllAsync();
        //    return View(requests);
        //}
        [Authorize(Roles = "Admin,Lecturer")]

        public async Task<IActionResult> PendingRequests()
        {
            // 1. Tạo câu truy vấn gốc (bao gồm luôn Include để lấy tên Giảng viên)
            var query = _context.AppRegistrationRequests
                .Include(r => r.AssignedLecturer)
                .AsQueryable();

            // 2. Kiểm tra Role và lọc dữ liệu
            // Nếu là Lecturer (và không phải Admin), chỉ lấy những request được gán cho chính họ
            if (User.IsInRole("Lecturer") && !User.IsInRole("Admin"))
            {
                var currentUserId = _userManager.GetUserId(User);
                query = query.Where(r => r.AssignedLecturerId == currentUserId);
            }

            // 3. Thực thi truy vấn và sắp xếp (Pending -> Processing -> Approved)
            var allRequests = await query
                .OrderBy(r => r.Status)
                .ThenByDescending(r => r.RequestId)
                .ToListAsync();

            // 4. Lấy danh sách Lecturer cho Dropdown (Chỉ cần thiết nếu là Admin)
            if (User.IsInRole("Admin"))
            {
                var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
                ViewBag.LecturerList = new SelectList(lecturers, "Id", "FullName");
            }

            return View(allRequests);
        }        // 2. Action xử lý khi Admin submit gán Lecturer
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> AssignLecturer(string requestId, string lecturerId)
        {
            var request = await _context.AppRegistrationRequests.FindAsync(requestId);

            if (request != null && !string.IsNullOrEmpty(lecturerId))
            {
                request.AssignedLecturerId = lecturerId;

                request.Status = RequestStatus.Processing;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã gán Lecturer thành công!";
            }

            return RedirectToAction(nameof(PendingRequests));
        }

        [Authorize(Roles = "Admin,Lecturer")]
        [HttpPost]
        public async Task<IActionResult> ApproveRequest(string requestId)
        {
            var request = await _context.AppRegistrationRequests.FindAsync(requestId);

            if (request != null)
            {
                request.Status = RequestStatus.Approved;

                if (string.IsNullOrEmpty(request.AssignedLecturerId))
                {
                    var currentUserId = _userManager.GetUserId(User);


                    if (!string.IsNullOrEmpty(currentUserId))
                    {
                        request.AssignedLecturerId = currentUserId;
                    }
                }
                await _emailService.SendConfirmAppAsync(request.StudentEmail, request.StudentInfo, request);

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã duyệt ứng dụng {request.AppName} thành công!";
            }

            return RedirectToAction(nameof(PendingRequests));
        }



    }
}
