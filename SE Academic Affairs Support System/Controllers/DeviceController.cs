using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.Services.Email;

namespace SE_Academic_Affairs_Support_System.Controllers
{
    public class DeviceController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly EmailService _emailService;
        public DeviceController(AppDbContext context)
        {
            _context = context;
            _emailService = new EmailService(_config);

        }


        public IActionResult Index(string filterStatus = null, string filterCategory = null, string filterCondition = null)
        {
            var devices = _context.Devices.AsQueryable();

            if (!string.IsNullOrEmpty(filterStatus))
                devices = devices.Where(d => d.Status == filterStatus);

            if (!string.IsNullOrEmpty(filterCategory))
                devices = devices.Where(d => d.Category == filterCategory);

            if (!string.IsNullOrEmpty(filterCondition))
                devices = devices.Where(d => d.Condition == filterCondition);

            ViewBag.FilterStatus = filterStatus;
            ViewBag.FilterCategory = filterCategory;
            ViewBag.FilterCondition = filterCondition;
            ViewBag.Categories = _context.Devices
                .Where(d => d.Category != null)
                .Select(d => d.Category)
                .Distinct()
                .ToList();
            ViewBag.PendingCount = _context.DeviceRequests.Count(r => r.Status == "Pending");

            return View(devices.ToList());
        }



        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Device device)
        {
            if (ModelState.IsValid)
            {
                device.Status = "Available";
                device.Condition ??= "Good";
                _context.Devices.Add(device);
                _context.SaveChanges();
                TempData["Success"] = $"Đã thêm thiết bị \"{device.DeviceName}\" thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(device);
        }

        public IActionResult Edit(int id)
        {
            var device = _context.Devices.Find(id);
            if (device == null) return NotFound();
            return View(device);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Device device)
        {
            if (ModelState.IsValid)
            {
                var existing = _context.Devices.Find(id);
                if (existing == null) return NotFound();

                existing.DeviceName = device.DeviceName;
                existing.Category = device.Category;
                existing.Condition = device.Condition;
                existing.Description = device.Description;
                existing.ImageUrl = device.ImageUrl;

                _context.SaveChanges();
                TempData["Success"] = $"Đã cập nhật thiết bị \"{device.DeviceName}\"!";
                return RedirectToAction(nameof(Index));
            }
            return View(device);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var device = _context.Devices.Find(id);
            if (device == null) return NotFound();

            if (device.Status == "Borrowing")
            {
                TempData["Error"] = "Không thể xóa thiết bị đang được mượn!";
                return RedirectToAction(nameof(Index));
            }

            _context.Devices.Remove(device);
            _context.SaveChanges();
            TempData["Success"] = $"Đã xóa thiết bị \"{device.DeviceName}\"!";
            return RedirectToAction(nameof(Index));
        }


        public IActionResult BorrowRequests()
        {
            var requests = _context.DeviceRequests
                .Include(r => r.Device)
                .OrderByDescending(r => r.RequestDate)
                .Select(r => new DeviceRequestViewModel
                {
                    Request = r,
                    Device = r.Device
                })
                .ToList();

            return View(requests);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRequest(int id)
        {
            // Lấy thông tin request và device đi kèm
            var request = _context.DeviceRequests
                .Include(r => r.Device)
                .FirstOrDefault(r => r.RequestId == id);

            if (request == null) return NotFound();

            // Kiểm tra xem thiết bị có thực sự còn rảnh không
            if (request.Device.Status != "Available")
            {
                TempData["Error"] = $"Thiết bị \"{request.Device.DeviceName}\" không còn sẵn sàng để cho mượn.";
                return RedirectToAction(nameof(BorrowRequests));
            }

            // 1. Duyệt yêu cầu hiện tại và đổi trạng thái thiết bị
            request.Status = "Approved";
            request.Device.Status = "Borrowing";

            // 2. Tìm tất cả các yêu cầu CHỜ DUYỆT (Pending) khác của CÙNG thiết bị này
            var conflictingRequests = _context.DeviceRequests
                .Where(r => r.DeviceId == request.DeviceId
                         && r.RequestId != id
                         && r.Status == "Pending")
                .ToList();

            // 3. Tự động từ chối các yêu cầu trùng lặp
            foreach (var conflict in conflictingRequests)
            {
                conflict.Status = "Rejected";

            }

            // Lưu toàn bộ thay đổi (bao gồm đơn được duyệt, trạng thái thiết bị, và các đơn bị từ chối) vào DB
            _context.SaveChanges();

            // Thêm thông báo chi tiết hơn một chút để người quản lý biết hệ thống vừa làm gì
            string autoRejectMsg = conflictingRequests.Any()
                ? $" Đồng thời tự động từ chối {conflictingRequests.Count} yêu cầu trùng lặp."
                : "";
            await _emailService.SendConfirmDeviceAsync(request.BorrowerEmail,request.BorrowerName,request);
            TempData["Success"] = $"Đã duyệt yêu cầu mượn của {request.BorrowerName} — {request.Device.DeviceName}.{autoRejectMsg}";

            return RedirectToAction(nameof(BorrowRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RejectRequest(int id, string rejectReason = "") // Đổi requestId thành id
        {
            // Load kèm Device để lấy tên thiết bị hiển thị thông báo nếu cần (tùy chọn)
            var request = _context.DeviceRequests.Find(id);

            if (request == null) return NotFound();

            request.Status = "Rejected";
            request.RejectReason = rejectReason;
            _context.SaveChanges();

            TempData["Success"] = $"Đã từ chối yêu cầu mượn của {request.BorrowerName}.";
            return RedirectToAction(nameof(BorrowRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkReturned(int id)
        {
            var device = _context.Devices.Find(id);
            if (device == null) return NotFound();

            if (device.Status != "Borrowing")
            {
                TempData["Error"] = "Chỉ có thể đánh dấu \"Đã trả\" khi thiết bị đang ở trạng thái mượn.";
                return RedirectToAction(nameof(Index));
            }

            device.Status = "Returned";

            var activeRequest = _context.DeviceRequests
                .Where(r => r.DeviceId == id && r.Status == "Approved")
                .OrderByDescending(r => r.RequestDate)
                .FirstOrDefault();

            if (activeRequest != null)
            {
                activeRequest.Status = "Returned";
                activeRequest.ReturnDate = System.DateTime.Now;
            }

            _context.SaveChanges();
            TempData["Success"] = $"Thiết bị \"{device.DeviceName}\" đã được đánh dấu là Đã trả.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAvailable(int id)
        {
            var device = _context.Devices.Find(id);
            if (device == null) return NotFound();

            if (device.Status != "Returned")
            {
                TempData["Error"] = "Chỉ có thể chuyển về \"Sẵn sàng\" sau khi thiết bị đã được trả.";
                return RedirectToAction(nameof(Index));
            }

            device.Status = "Available";
            _context.SaveChanges();

            TempData["Success"] = $"Thiết bị \"{device.DeviceName}\" đã sẵn sàng cho mượn.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangeCondition(int id, string condition)
        {
            var device = _context.Devices.Find(id);
            if (device == null) return NotFound();

            if (condition != "Good" && condition != "Broken") return BadRequest();

            device.Condition = condition;
            _context.SaveChanges();

            TempData["Success"] = $"Đã cập nhật tình trạng thiết bị \"{device.DeviceName}\" thành {(condition == "Good" ? "Tốt" : "Hỏng")}.";
            return RedirectToAction(nameof(Index));
        }
        public IActionResult BorrowForm(int? deviceId = null)
        {
            // Chỉ lấy thiết bị Available VÀ Condition = Good
            var availableDevices = _context.Devices
                .Where(d => d.Status == "Available" && d.Condition == "Good")
                .OrderBy(d => d.Category)
                .ThenBy(d => d.DeviceName)
                .ToList();

            ViewBag.AvailableDevices = availableDevices;
            ViewBag.SelectedDeviceId = deviceId;

            // Nếu có deviceId truyền vào, pre-select thiết bị đó
            if (deviceId.HasValue)
            {
                var preSelected = availableDevices.FirstOrDefault(d => d.DeviceId == deviceId.Value);
                ViewBag.PreSelectedDevice = preSelected;
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BorrowForm(DeviceRequest request)
        {
            // Kiểm tra thiết bị còn available không (double-check server side)
            var device = _context.Devices.Find(request.DeviceId);

            if (device == null || device.Status != "Available" || device.Condition != "Good")
            {
                ModelState.AddModelError("DeviceId", "Thiết bị này hiện không còn sẵn sàng để mượn. Vui lòng chọn thiết bị khác.");
            }


            if (ModelState.IsValid)
            {
                request.Status = "Pending";
                request.RequestDate = System.DateTime.Now;
                _context.DeviceRequests.Add(request);
                _context.SaveChanges();

                return RedirectToAction(nameof(BorrowSuccess), new { requestId = request.RequestId });
            }

            // Reload available devices nếu có lỗi
            ViewBag.AvailableDevices = _context.Devices
                .Where(d => d.Status == "Available" && d.Condition == "Good")
                .OrderBy(d => d.Category)
                .ThenBy(d => d.DeviceName)
                .ToList();

            ViewBag.SelectedDeviceId = request.DeviceId;
            ViewBag.preSelectedDevice = _context.Devices.Find(request.DeviceId);
            return View(request);
        }

        public IActionResult BorrowSuccess(int requestId)
        {
            var request = _context.DeviceRequests
                .Include(r => r.Device)
                .FirstOrDefault(r => r.RequestId == requestId);

            if (request == null) return RedirectToAction(nameof(BorrowForm));
            return View(request);
        }
        public IActionResult Catalog()
        {
            var availableDevices = _context.Devices
                .Where(d => d.Status == "Available" && d.Condition == "Good")
                .OrderBy(d => d.Category)
                .ThenBy(d => d.DeviceName)
                .ToList();

            ViewBag.Categories = availableDevices
                .Where(d => d.Category != null)
                .Select(d => d.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            ViewBag.TotalAvailable = availableDevices.Count;

            return View(availableDevices);
        }
    }
}