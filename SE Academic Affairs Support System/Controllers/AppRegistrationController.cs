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
using SE_Academic_Affairs_Support_System.Services.EmailNotification;
using SE_Academic_Affairs_Support_System.Services.Excel;
using SE_Academic_Affairs_Support_System.Services.NotificationSevices;
using SE_Academic_Affairs_Support_System.ViewModels;


namespace SE_Academic_Affairs_Support_System.Controllers
{
    public class AppRegistrationController : Controller
    {
        private readonly IAppRegistrationService _service;
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IEmailNotificationService _emailNotif;
        private readonly IExcelService _excel;
        private readonly INotificationService _notif;

        public AppRegistrationController(IAppRegistrationService service, AppDbContext context,
            UserManager<User> userManager, IEmailNotificationService emailNotif, IExcelService excel,
            INotificationService notif)
        {
            _service = service;
            _context = context;
            _userManager = userManager;
            _emailNotif = emailNotif;
            _excel = excel;
            _notif = notif;
        }

        private static string StatusToVietnamese(RequestStatus status) => status switch
        {
            RequestStatus.Pending => "Chờ xử lý",
            RequestStatus.Processing => "Đang xử lý",
            RequestStatus.Approved => "Đã duyệt",
            RequestStatus.Rejected => "Từ chối",
            _ => status.ToString()
        };

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

            model.CreatedAt = DateTime.Now;
            await _service.CreateRequestAsync(model);

            await _emailNotif.NotifyAppSubmittedAsync(
                model.StudentEmail, model.StudentInfo ?? string.Empty,
                model.AppName, model.RequestId);

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

                // Thông báo cho giảng viên được phân công: in-app + email (fail-safe)
                var lecturer = await _userManager.FindByIdAsync(lecturerId);
                if (lecturer != null)
                {
                    await _notif.SendAsync(lecturer.Id,
                        $"Bạn được phân công xử lý yêu cầu đăng tải app \"{request.AppName}\".",
                        "/AppRegistration/PendingRequests");

                    await _emailNotif.NotifyAppAssignedToLecturerAsync(
                        lecturer.Email ?? string.Empty,
                        lecturer.FullName ?? lecturer.UserName ?? "Giảng viên",
                        request.AppName ?? "(không tên)",
                        request.StudentInfo ?? string.Empty,
                        request.RequestId);
                }

                TempData["SuccessMessage"] = "Đã gán Giảng viên và gửi thông báo!";
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
                await _context.SaveChangesAsync();

                await _emailNotif.NotifyAppApprovedAsync(
                    request.StudentEmail, request.StudentInfo ?? string.Empty, request.AppName);

                TempData["SuccessMessage"] = $"Đã duyệt ứng dụng {request.AppName} thành công!";
            }

            return RedirectToAction(nameof(PendingRequests));
        }

        // Từ chối yêu cầu đăng tải app + gửi mail từ chối (fail-safe)
        [Authorize(Roles = "Admin,Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRequest(string requestId, string rejectReason)
        {
            var request = await _context.AppRegistrationRequests.FindAsync(requestId);

            if (request != null)
            {
                request.Status = RequestStatus.Rejected;
                await _context.SaveChangesAsync();

                await _emailNotif.NotifyAppRejectedAsync(
                    request.StudentEmail, request.StudentInfo ?? string.Empty, request.AppName,
                    string.IsNullOrWhiteSpace(rejectReason) ? null : rejectReason.Trim());

                TempData["SuccessMessage"] = $"Đã từ chối ứng dụng {request.AppName}.";
            }

            return RedirectToAction(nameof(PendingRequests));
        }

        // GET /AppRegistration/ExportExcel?TimeMode=&FromDate=&ToDate=&Status=
        // Xuất danh sách yêu cầu đăng tải app (CH Play) ra .xlsx theo bộ lọc thời gian + trạng thái.
        // Lọc thời gian áp lên CreatedAt (ngày gửi yêu cầu).
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> ExportExcel(ExportFilterViewModel filter)
        {
            var (start, end, error) = filter.ResolveRange();
            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(PendingRequests));
            }

            var query = _context.AppRegistrationRequests
                .Include(r => r.AssignedLecturer)
                .AsQueryable();

            // Lecturer chỉ xuất các yêu cầu được gán cho mình (giống PendingRequests)
            if (User.IsInRole("Lecturer") && !User.IsInRole("Admin"))
            {
                var currentUserId = _userManager.GetUserId(User);
                query = query.Where(r => r.AssignedLecturerId == currentUserId);
            }

            var statusSlug = "TatCa";
            if (!string.IsNullOrWhiteSpace(filter.Status) &&
                Enum.TryParse<RequestStatus>(filter.Status, true, out var statusEnum))
            {
                query = query.Where(r => r.Status == statusEnum);
                statusSlug = statusEnum.ToString();
            }

            if (start.HasValue) query = query.Where(r => r.CreatedAt >= start.Value);
            if (end.HasValue) query = query.Where(r => r.CreatedAt <= end.Value);

            var requests = await query
                .OrderBy(r => r.Status)
                .ThenByDescending(r => r.CreatedAt)
                .ToListAsync();

            var headers = new[]
            {
                "Mã yêu cầu", "Tên app", "Mô tả", "Mục đích",
                "Người yêu cầu", "Email", "Giảng viên phụ trách",
                "Demo link", "APK link", "Trạng thái", "Ngày yêu cầu"
            };

            var rows = requests.Select(r => new List<object?>
            {
                r.RequestId,
                r.AppName,
                r.AppDescription,
                r.Purpose,
                r.StudentInfo,
                r.StudentEmail,
                r.AssignedLecturer?.FullName ?? "Chưa gán",
                r.DemoLink,
                r.ApkLink,
                StatusToVietnamese(r.Status),
                r.CreatedAt
            } as IReadOnlyList<object?>).ToList();

            var bytes = _excel.BuildWorkbook("YeuCauApp", headers, rows);
            var fileName = $"yeu-cau-app_{statusSlug}_{filter.TimeModeSlug()}_{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
    }
}
