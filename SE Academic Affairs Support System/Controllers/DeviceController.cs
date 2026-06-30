using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.Services.Email;
using SE_Academic_Affairs_Support_System.Services.EmailNotification;
using SE_Academic_Affairs_Support_System.Services.Excel;
using SE_Academic_Affairs_Support_System.ViewModels;

namespace SE_Academic_Affairs_Support_System.Controllers
{
    public class DeviceController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailNotificationService _emailNotif;
        private readonly UserManager<User> _userManager;
        private readonly IExcelService _excel;

        public DeviceController(AppDbContext context, UserManager<User> userManager,
            IEmailNotificationService emailNotif, IExcelService excel)
        {
            _context = context;
            _emailNotif = emailNotif;
            _userManager = userManager;
            _excel = excel;
        }

        private static readonly string[] DeviceTemplateHeaders =
            { "Mã thiết bị", "Tên thiết bị", "Loại thiết bị", "Mô tả", "Link ảnh", "Tổng số lượng", "Số lượng hỏng" };

        // Số lượng ĐANG MƯỢN theo từng thiết bị = tổng Quantity của các item thuộc phiếu Approved.
        private Dictionary<int, int> GetBorrowedMap(IEnumerable<int>? deviceIds = null)
        {
            var q = _context.DeviceRequestItems.Where(i => i.Request.Status == "Approved");
            if (deviceIds != null)
            {
                var ids = deviceIds.ToList();
                q = q.Where(i => ids.Contains(i.DeviceId));
            }
            return q.GroupBy(i => i.DeviceId)
                    .Select(g => new { g.Key, Sum = g.Sum(x => x.Quantity) })
                    .ToDictionary(x => x.Key, x => x.Sum);
        }

        public IActionResult Index(string filterCategory = null, string availability = null)
        {
            var query = _context.Devices.AsQueryable();
            if (!string.IsNullOrEmpty(filterCategory))
                query = query.Where(d => d.Category == filterCategory);

            var devices = query.OrderBy(d => d.Category).ThenBy(d => d.DeviceName).ToList();
            var borrowed = GetBorrowedMap(devices.Select(d => d.DeviceId));

            var rows = devices.Select(d => new DeviceInventoryRow
            {
                Device = d,
                Borrowed = borrowed.GetValueOrDefault(d.DeviceId)
            }).ToList();

            if (availability == "Available") rows = rows.Where(r => r.Available > 0).ToList();
            else if (availability == "Out") rows = rows.Where(r => r.Available <= 0).ToList();

            ViewBag.FilterCategory = filterCategory;
            ViewBag.Availability = availability;
            ViewBag.Categories = _context.Devices
                .Where(d => d.Category != null)
                .Select(d => d.Category)
                .Distinct()
                .ToList();
            ViewBag.PendingCount = _context.DeviceRequests.Count(r => r.Status == "Pending");

            return View(rows);
        }



        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Device device)
        {
            NormalizeAndValidateCode(device, null);
            if (device.TotalQuantity < 1)
                ModelState.AddModelError(nameof(Device.TotalQuantity), "Tổng số lượng phải ít nhất là 1.");
            if (device.BrokenQuantity > device.TotalQuantity)
                ModelState.AddModelError(nameof(Device.BrokenQuantity), "Số lượng hỏng không được vượt quá tổng số lượng.");

            if (ModelState.IsValid)
            {
                _context.Devices.Add(device);
                _context.SaveChanges();
                TempData["Success"] = $"Đã thêm thiết bị \"{device.DeviceName}\" (SL: {device.TotalQuantity}) thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(device);
        }

        // Chuẩn hóa + kiểm tra trùng mã thiết bị (DeviceCode là unique, nullable)
        private void NormalizeAndValidateCode(Device device, int? excludeId)
        {
            if (string.IsNullOrWhiteSpace(device.DeviceCode))
            {
                device.DeviceCode = null;
                return;
            }

            device.DeviceCode = device.DeviceCode.Trim();
            bool dup = _context.Devices.Any(d => d.DeviceCode == device.DeviceCode
                && (excludeId == null || d.DeviceId != excludeId.Value));
            if (dup)
                ModelState.AddModelError(nameof(Device.DeviceCode),
                    $"Mã thiết bị \"{device.DeviceCode}\" đã tồn tại.");
        }

        public IActionResult Edit(int id)
        {
            var device = _context.Devices.Find(id);
            if (device == null) return NotFound();
            ViewBag.Borrowed = GetBorrowedMap(new[] { id }).GetValueOrDefault(id);
            return View(device);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Device device)
        {
            NormalizeAndValidateCode(device, id);

            int borrowed = GetBorrowedMap(new[] { id }).GetValueOrDefault(id);
            if (device.TotalQuantity < borrowed)
                ModelState.AddModelError(nameof(Device.TotalQuantity),
                    $"Tổng số lượng không được nhỏ hơn số đang được mượn ({borrowed}).");
            if (device.BrokenQuantity < 0) device.BrokenQuantity = 0;
            if (device.BrokenQuantity > device.TotalQuantity - borrowed)
                ModelState.AddModelError(nameof(Device.BrokenQuantity),
                    $"Số hỏng cộng số đang mượn ({borrowed}) không được vượt quá tổng số lượng.");

            if (ModelState.IsValid)
            {
                var existing = _context.Devices.Find(id);
                if (existing == null) return NotFound();

                existing.DeviceCode = device.DeviceCode;
                existing.DeviceName = device.DeviceName;
                existing.Category = device.Category;
                existing.Description = device.Description;
                existing.ImageUrl = device.ImageUrl;
                existing.TotalQuantity = device.TotalQuantity;
                existing.BrokenQuantity = device.BrokenQuantity;

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

            int borrowedNow = GetBorrowedMap(new[] { id }).GetValueOrDefault(id);
            if (borrowedNow > 0)
            {
                TempData["Error"] = $"Không thể xóa \"{device.DeviceName}\" vì còn {borrowedNow} đang được mượn.";
                return RedirectToAction(nameof(Index));
            }

            // Thiết bị đã/đang nằm trong phiếu mượn → FK Restrict sẽ chặn; báo lỗi thân thiện
            bool inUse = _context.DeviceRequestItems.Any(i => i.DeviceId == id);
            if (inUse)
            {
                TempData["Error"] = $"Không thể xóa \"{device.DeviceName}\" vì thiết bị đã nằm trong (các) phiếu mượn. Hãy xóa các phiếu liên quan trước.";
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
                .Include(r => r.Items).ThenInclude(i => i.Device)
                .OrderByDescending(r => r.RequestDate)
                .ToList();

            return View(requests);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRequest(int id)
        {
            var request = await _context.DeviceRequests
                .Include(r => r.Items).ThenInclude(i => i.Device)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null) return NotFound();
            if (request.Status != "Pending")
            {
                TempData["Error"] = "Yêu cầu này đã được xử lý.";
                return RedirectToAction(nameof(BorrowRequests));
            }

            // Kiểm tra TỒN KHO đủ cho từng thiết bị: cần ≤ (tổng − hỏng − đang mượn bởi phiếu khác)
            var deviceIds = request.Items.Select(i => i.DeviceId).Distinct().ToList();
            var borrowed = GetBorrowedMap(deviceIds);
            var shortage = new List<string>();
            foreach (var item in request.Items)
            {
                if (item.Device == null) { shortage.Add("(thiết bị đã xóa)"); continue; }
                int avail = item.Device.TotalQuantity - item.Device.BrokenQuantity - borrowed.GetValueOrDefault(item.DeviceId);
                if (item.Quantity > avail)
                    shortage.Add($"{item.Device.DeviceName} (cần {item.Quantity}, còn {Math.Max(avail, 0)})");
            }
            if (shortage.Any())
            {
                TempData["Error"] = $"Không đủ tồn kho để duyệt: {string.Join("; ", shortage)}.";
                return RedirectToAction(nameof(BorrowRequests));
            }

            request.Status = "Approved";
            await _context.SaveChangesAsync();

            var deviceNames = string.Join(", ", request.Items.Select(i => i.Device?.DeviceName ?? "thiết bị"));
            await _emailNotif.NotifyDeviceBorrowApprovedAsync(
                request.BorrowerEmail, request.BorrowerName,
                deviceNames, request.Purpose ?? string.Empty);

            TempData["Success"] = $"Đã duyệt yêu cầu mượn của {request.BorrowerName} — {deviceNames}.";
            return RedirectToAction(nameof(BorrowRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRequest(int id, string rejectReason = "")
        {
            var request = await _context.DeviceRequests
                .Include(r => r.Items).ThenInclude(i => i.Device)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null) return NotFound();

            request.Status = "Rejected";
            request.RejectReason = rejectReason;
            await _context.SaveChangesAsync();

            var deviceNames = string.Join(", ", request.Items.Select(i => i.Device?.DeviceName ?? "thiết bị"));
            await _emailNotif.NotifyDeviceBorrowRejectedAsync(
                request.BorrowerEmail, request.BorrowerName,
                string.IsNullOrWhiteSpace(deviceNames) ? "thiết bị" : deviceNames, rejectReason);

            TempData["Success"] = $"Đã từ chối yêu cầu mượn của {request.BorrowerName}.";
            return RedirectToAction(nameof(BorrowRequests));
        }

        // GET /Device/ReturnForm/{id} — màn hình nhận trả + ghi nhận hư hỏng theo từng thiết bị.
        public async Task<IActionResult> ReturnForm(int id)
        {
            var request = await _context.DeviceRequests
                .Include(r => r.Items).ThenInclude(i => i.Device)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null) return NotFound();
            if (request.Status != "Approved")
            {
                TempData["Error"] = "Chỉ có thể trả phiếu đang ở trạng thái Đã duyệt.";
                return RedirectToAction(nameof(BorrowRequests));
            }

            var vm = new ReturnFormViewModel
            {
                RequestId = request.RequestId,
                BorrowerName = request.BorrowerName,
                Items = request.Items.Select(i => new ReturnItemInput
                {
                    DeviceId = i.DeviceId,
                    DeviceName = i.Device?.DeviceName ?? "(đã xóa)",
                    BorrowedQuantity = i.Quantity,
                    DamagedQuantity = 0,
                    DamagedByName = request.BorrowerName
                }).ToList()
            };
            return View(vm);
        }

        // POST /Device/ReturnRequest — đánh dấu cả phiếu đã trả; ghi nhận hư hỏng (nếu có).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnRequest(ReturnFormViewModel vm)
        {
            var request = await _context.DeviceRequests
                .Include(r => r.Items).ThenInclude(i => i.Device)
                .FirstOrDefaultAsync(r => r.RequestId == vm.RequestId);

            if (request == null) return NotFound();
            if (request.Status != "Approved")
            {
                TempData["Error"] = "Chỉ có thể trả phiếu đang ở trạng thái Đã duyệt.";
                return RedirectToAction(nameof(BorrowRequests));
            }

            var borrowedByDevice = request.Items
                .GroupBy(i => i.DeviceId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var items = vm.Items ?? new List<ReturnItemInput>();
            foreach (var item in items)
            {
                if (item.DamagedQuantity < 0) item.DamagedQuantity = 0;
                int borrowedQ = borrowedByDevice.GetValueOrDefault(item.DeviceId);
                if (item.DamagedQuantity > borrowedQ)
                    ModelState.AddModelError(string.Empty,
                        $"\"{item.DeviceName}\": số hỏng ({item.DamagedQuantity}) vượt quá số đã mượn ({borrowedQ}).");
                if (item.DamagedQuantity > 0 && string.IsNullOrWhiteSpace(item.Reason))
                    ModelState.AddModelError(string.Empty, $"\"{item.DeviceName}\": vui lòng nhập lý do hư hỏng.");
            }

            if (!ModelState.IsValid)
            {
                vm.BorrowerName = request.BorrowerName;
                foreach (var item in items)
                {
                    var src = request.Items.FirstOrDefault(i => i.DeviceId == item.DeviceId);
                    item.DeviceName = src?.Device?.DeviceName ?? item.DeviceName;
                    item.BorrowedQuantity = borrowedByDevice.GetValueOrDefault(item.DeviceId);
                }
                return View("ReturnForm", vm);
            }

            var deviceMap = request.Items
                .Where(i => i.Device != null)
                .GroupBy(i => i.DeviceId)
                .ToDictionary(g => g.Key, g => g.First().Device!);

            int damagedTotal = 0;
            var damageLines = new List<string>();
            foreach (var item in items)
            {
                if (item.DamagedQuantity <= 0) continue;
                damagedTotal += item.DamagedQuantity;

                var dname = deviceMap.TryGetValue(item.DeviceId, out var dv) ? dv.DeviceName : item.DeviceName;
                _context.DeviceDamageReports.Add(new DeviceDamageReport
                {
                    RequestId = request.RequestId,
                    DeviceId = item.DeviceId,
                    DeviceName = dname,
                    BorrowerName = request.BorrowerName,
                    DamagedByName = string.IsNullOrWhiteSpace(item.DamagedByName) ? request.BorrowerName : item.DamagedByName.Trim(),
                    Quantity = item.DamagedQuantity,
                    Reason = item.Reason?.Trim(),
                    ReportedAt = DateTime.Now
                });

                damageLines.Add($"{dname} — SL hỏng: {item.DamagedQuantity}"
                    + (string.IsNullOrWhiteSpace(item.Reason) ? "" : $", lý do: {item.Reason.Trim()}"));

                // Số hỏng cộng vào tồn kho hỏng của thiết bị (giảm số còn lại)
                if (deviceMap.TryGetValue(item.DeviceId, out var dev))
                    dev.BrokenQuantity = Math.Min(dev.TotalQuantity, dev.BrokenQuantity + item.DamagedQuantity);
            }

            request.Status = "Returned";
            request.ReturnDate = DateTime.Now;
            await _context.SaveChangesAsync();

            var deviceNames = string.Join(", ", request.Items.Select(i => i.Device?.DeviceName ?? "thiết bị"));
            await _emailNotif.NotifyDeviceReturnedAsync(
                request.BorrowerEmail, request.BorrowerName, deviceNames);

            // Mail báo sinh viên về thiết bị bàn giao bị hư hỏng (nếu có)
            if (damagedTotal > 0)
                await _emailNotif.NotifyDeviceDamagedAsync(
                    request.BorrowerEmail, request.BorrowerName, string.Join("<br/>", damageLines));

            TempData["Success"] = damagedTotal > 0
                ? $"Đã ghi nhận trả thiết bị của {request.BorrowerName}, kèm {damagedTotal} đơn vị hư hỏng."
                : $"Đã ghi nhận trả thiết bị của {request.BorrowerName}.";
            return RedirectToAction(nameof(BorrowRequests));
        }

        // ── Báo cáo hư hỏng (Admin) ───────────────────────────────────────────
        // GET /Device/DamageReports
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DamageReports()
        {
            var reports = await _context.DeviceDamageReports
                .OrderByDescending(r => r.ReportedAt)
                .ToListAsync();
            return View(reports);
        }

        // GET /Device/ExportDamageReports — xuất Excel danh sách hư hỏng
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportDamageReports()
        {
            var reports = await _context.DeviceDamageReports
                .OrderByDescending(r => r.ReportedAt)
                .ToListAsync();

            var headers = new[] { "Thiết bị", "Người mượn", "Người làm hư hỏng", "Số lượng hỏng", "Lý do hư hỏng", "Ngày ghi nhận" };
            var rows = reports.Select(r => new List<object?>
            {
                r.DeviceName,
                r.BorrowerName,
                r.DamagedByName,
                r.Quantity,
                r.Reason ?? "",
                r.ReportedAt
            } as IReadOnlyList<object?>).ToList();

            var bytes = _excel.BuildWorkbook("BaoCaoHuHong", headers, rows);
            var fileName = $"bao-cao-hu-hong-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // Quản lý hư hỏng theo SỐ LƯỢNG: đặt số đơn vị bị hỏng (tăng = đánh dấu hỏng, giảm = đã sửa).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetBroken(int id, int brokenQuantity)
        {
            var device = _context.Devices.Find(id);
            if (device == null) return NotFound();

            if (brokenQuantity < 0) brokenQuantity = 0;

            int borrowed = GetBorrowedMap(new[] { id }).GetValueOrDefault(id);
            int maxBroken = device.TotalQuantity - borrowed;
            if (brokenQuantity > maxBroken)
            {
                TempData["Error"] = $"Số hỏng tối đa là {Math.Max(maxBroken, 0)} (tổng {device.TotalQuantity} − đang mượn {borrowed}).";
                return RedirectToAction(nameof(Index));
            }

            device.BrokenQuantity = brokenQuantity;
            _context.SaveChanges();

            TempData["Success"] = $"Đã cập nhật số lượng hỏng của \"{device.DeviceName}\" thành {brokenQuantity}.";
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> BorrowForm(int? deviceId = null)
        {
            var user = await _userManager.GetUserAsync(User);

            var vm = new CreateBorrowViewModel
            {
                BorrowerName = user?.FullName ?? "",
                BorrowerEmail = user?.Email ?? "",
                Items = BuildBorrowItems(selectedMap: null, preSelectDeviceId: deviceId)
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BorrowForm(CreateBorrowViewModel vm)
        {
            var selected = (vm.Items ?? new()).Where(i => i.Selected).ToList();

            if (selected.Count == 0)
                ModelState.AddModelError(string.Empty, "Vui lòng chọn ít nhất một thiết bị để mượn.");

            // Double-check server side: còn đủ tồn kho cho từng thiết bị được chọn
            var deviceIds = selected.Select(i => i.DeviceId).Distinct().ToList();
            var devices = await _context.Devices
                .Where(d => deviceIds.Contains(d.DeviceId))
                .ToDictionaryAsync(d => d.DeviceId);
            var borrowed = GetBorrowedMap(deviceIds);

            foreach (var item in selected)
            {
                if (!devices.TryGetValue(item.DeviceId, out var dev))
                {
                    ModelState.AddModelError(string.Empty, $"Thiết bị \"{item.DeviceName}\" không tồn tại.");
                    continue;
                }
                int avail = dev.TotalQuantity - dev.BrokenQuantity - borrowed.GetValueOrDefault(item.DeviceId);
                if (item.Quantity < 1)
                    ModelState.AddModelError(string.Empty, $"Số lượng cho \"{dev.DeviceName}\" phải lớn hơn 0.");
                else if (item.Quantity > avail)
                    ModelState.AddModelError(string.Empty, $"\"{dev.DeviceName}\" chỉ còn {Math.Max(avail, 0)} có thể mượn.");
            }

            if (!ModelState.IsValid)
            {
                var selMap = selected.GroupBy(i => i.DeviceId).ToDictionary(g => g.Key, g => g.First().Quantity);
                vm.Items = BuildBorrowItems(selMap, preSelectDeviceId: null);
                return View(vm);
            }

            var request = new DeviceRequest
            {
                BorrowerName = vm.BorrowerName,
                BorrowerEmail = vm.BorrowerEmail,
                Purpose = vm.Purpose,
                Status = "Pending",
                RequestDate = DateTime.Now,
                Items = selected.Select(i => new DeviceRequestItem
                {
                    DeviceId = i.DeviceId,
                    Quantity = i.Quantity < 1 ? 1 : i.Quantity
                }).ToList()
            };
            _context.DeviceRequests.Add(request);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(BorrowSuccess), new { requestId = request.RequestId });
        }

        // Dựng danh sách thiết bị CÒN HÀNG (Available > 0) cho form mượn, giữ lựa chọn/số lượng nếu có.
        private List<BorrowItemInput> BuildBorrowItems(Dictionary<int, int>? selectedMap, int? preSelectDeviceId)
        {
            var devices = _context.Devices
                .OrderBy(d => d.Category).ThenBy(d => d.DeviceName).ToList();
            var borrowed = GetBorrowedMap(devices.Select(d => d.DeviceId));

            return devices
                .Select(d => new { d, Avail = d.TotalQuantity - d.BrokenQuantity - borrowed.GetValueOrDefault(d.DeviceId) })
                .Where(x => x.Avail > 0)
                .Select(x =>
                {
                    bool sel = selectedMap?.ContainsKey(x.d.DeviceId) == true
                               || (preSelectDeviceId.HasValue && x.d.DeviceId == preSelectDeviceId.Value);
                    int qty = selectedMap != null && selectedMap.TryGetValue(x.d.DeviceId, out var q) && q >= 1 ? q : 1;
                    return new BorrowItemInput
                    {
                        DeviceId = x.d.DeviceId,
                        DeviceName = x.d.DeviceName,
                        Category = x.d.Category,
                        Available = x.Avail,
                        Selected = sel,
                        Quantity = Math.Min(qty, x.Avail)
                    };
                }).ToList();
        }

        public IActionResult BorrowSuccess(int requestId)
        {
            var request = _context.DeviceRequests
                .Include(r => r.Items).ThenInclude(i => i.Device)
                .FirstOrDefault(r => r.RequestId == requestId);

            if (request == null) return RedirectToAction(nameof(BorrowForm));
            return View(request);
        }
        public IActionResult Catalog()
        {
            var devices = _context.Devices
                .OrderBy(d => d.Category).ThenBy(d => d.DeviceName).ToList();
            var borrowed = GetBorrowedMap(devices.Select(d => d.DeviceId));

            var rows = devices
                .Select(d => new DeviceInventoryRow { Device = d, Borrowed = borrowed.GetValueOrDefault(d.DeviceId) })
                .Where(r => r.Available > 0)
                .ToList();

            ViewBag.Categories = rows
                .Where(r => r.Device.Category != null)
                .Select(r => r.Device.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            ViewBag.TotalAvailable = rows.Count;

            return View(rows);
        }

        // ── Import thiết bị hàng loạt (Admin) ─────────────────────────────────
        // GET /Device/ImportDevices
        [Authorize(Roles = "Admin")]
        public IActionResult ImportDevices()
        {
            return View(new ImportDevicesViewModel());
        }

        // POST /Device/ImportDevices
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportDevices(ImportDevicesViewModel vm)
        {
            var fileError = _excel.ValidateUploadedFile(vm.File);
            if (fileError != null)
            {
                ModelState.AddModelError(nameof(vm.File), fileError);
                return View(vm);
            }

            var (created, skipped, errors) = await ProcessDeviceImportAsync(vm.File!);
            vm.Created = created;
            vm.Skipped = skipped;
            vm.Errors = errors;
            vm.IsProcessed = true;
            vm.File = null;

            if (created > 0)
                TempData["Success"] = $"Đã import {created} thiết bị vào danh mục.";

            return View(vm);
        }

        // GET /Device/DownloadDeviceTemplate
        [Authorize(Roles = "Admin")]
        public IActionResult DownloadDeviceTemplate()
        {
            var sample = new[] { "MC01", "Máy chiếu Panasonic", "Máy chiếu", "Độ phân giải Full HD", "", "3", "0" };
            var bytes = _excel.BuildTemplate("ThietBi", DeviceTemplateHeaders, sample);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "mau-import-thiet-bi.xlsx");
        }

        // Cột file: 1=Mã*, 2=Tên*, 3=Loại, 4=Mô tả, 5=Link ảnh, 6=Tổng số lượng, 7=Số lượng hỏng
        // Mỗi dòng = MỘT thiết bị với tồn kho riêng (không còn nhân bản N dòng).
        // Toàn bộ thiết bị hợp lệ được lưu trong 1 transaction (all-or-nothing).
        private async Task<(int Created, int Skipped, List<string> Errors)> ProcessDeviceImportAsync(IFormFile file)
        {
            var errors = new List<string>();

            List<ExcelRow> rows;
            try
            {
                rows = _excel.ReadRows(file, 7);
            }
            catch (ExcelReadException ex)
            {
                return (0, 0, new List<string> { ex.Message });
            }

            if (rows.Count == 0)
                return (0, 0, new List<string> { "File không có dòng dữ liệu nào (chỉ có tiêu đề)." });

            using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                // Khóa phạm vi mã thiết bị hiện có để chống race khi import đồng thời
                var existingCodes = (await _context.Devices
                        .FromSqlRaw("SELECT * FROM Devices WITH (UPDLOCK, HOLDLOCK) WHERE DeviceCode IS NOT NULL")
                        .ToListAsync())
                    .Select(d => d.DeviceCode!.Trim().ToLowerInvariant())
                    .ToHashSet();

                var toCreate = new List<Device>();
                var seenCodes = new HashSet<string>();

                foreach (var row in rows)
                {
                    var code = row.Get(0).Trim();
                    var name = row.Get(1).Trim();

                    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                    {
                        errors.Add($"Dòng {row.RowNumber}: thiếu Mã thiết bị hoặc Tên thiết bị, bỏ qua.");
                        continue;
                    }
                    if (code.Length > 50)
                    {
                        errors.Add($"Dòng {row.RowNumber}: Mã thiết bị vượt quá 50 ký tự, bỏ qua.");
                        continue;
                    }

                    var totalRaw = row.Get(5).Trim();
                    int total = 1;
                    if (!string.IsNullOrWhiteSpace(totalRaw) &&
                        (!int.TryParse(totalRaw, out total) || total < 1 || total > 100000))
                    {
                        errors.Add($"Dòng {row.RowNumber}: Tổng số lượng \"{totalRaw}\" không hợp lệ (1–100000), bỏ qua.");
                        continue;
                    }

                    var brokenRaw = row.Get(6).Trim();
                    int broken = 0;
                    if (!string.IsNullOrWhiteSpace(brokenRaw) &&
                        (!int.TryParse(brokenRaw, out broken) || broken < 0))
                    {
                        errors.Add($"Dòng {row.RowNumber}: Số lượng hỏng \"{brokenRaw}\" không hợp lệ, bỏ qua.");
                        continue;
                    }
                    if (broken > total)
                    {
                        errors.Add($"Dòng {row.RowNumber}: Số lượng hỏng ({broken}) vượt quá tổng ({total}), bỏ qua.");
                        continue;
                    }

                    var key = code.ToLowerInvariant();
                    if (existingCodes.Contains(key) || seenCodes.Contains(key))
                    {
                        errors.Add($"Dòng {row.RowNumber}: Mã thiết bị \"{code}\" đã tồn tại (trong DB hoặc trùng trong file), bỏ qua.");
                        continue;
                    }
                    seenCodes.Add(key);

                    toCreate.Add(new Device
                    {
                        DeviceCode = code,
                        DeviceName = name,
                        Category = string.IsNullOrWhiteSpace(row.Get(2)) ? null : row.Get(2).Trim(),
                        Description = string.IsNullOrWhiteSpace(row.Get(3)) ? null : row.Get(3).Trim(),
                        ImageUrl = string.IsNullOrWhiteSpace(row.Get(4)) ? null : row.Get(4).Trim(),
                        TotalQuantity = total,
                        BrokenQuantity = broken
                    });
                }

                int skipped = rows.Count - toCreate.Count;
                if (toCreate.Count == 0)
                {
                    await tx.RollbackAsync();
                    return (0, skipped, errors);
                }

                _context.Devices.AddRange(toCreate);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return (toCreate.Count, skipped, errors);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        // ── Export danh sách thiết bị đã mượn ra Excel (Admin) ────────────────
        // GET /Device/ExportBorrowed?TimeMode=&FromDate=&ToDate=&Status=
        // Phạm vi: các phiếu đã mượn thật sự (Status Approved/Returned).
        // Lọc thời gian áp lên RequestDate (thời gian mượn); trạng thái suy ra từ ReturnDate.
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportBorrowed(ExportFilterViewModel filter)
        {
            var (start, end, error) = filter.ResolveRange();
            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(BorrowRequests));
            }

            var query = _context.DeviceRequests
                .Where(r => r.Status == "Approved" || r.Status == "Returned");

            // Trạng thái trả/chưa trả ở MỨC PHIẾU, suy ra từ ReturnDate (null = chưa trả)
            var statusSlug = "TatCa";
            if (filter.Status == "Returned")
            {
                query = query.Where(r => r.ReturnDate != null);
                statusSlug = "DaTra";
            }
            else if (filter.Status == "NotReturned")
            {
                query = query.Where(r => r.ReturnDate == null);
                statusSlug = "ChuaTra";
            }

            if (start.HasValue) query = query.Where(r => r.RequestDate >= start.Value);
            if (end.HasValue) query = query.Where(r => r.RequestDate <= end.Value);

            var requests = await query
                .Include(r => r.Items).ThenInclude(i => i.Device)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            var headers = new[] { "Tên người mượn", "Thiết bị", "Người cho mượn", "Thời gian mượn", "Thời gian trả", "Số lượng" };
            const string lender = "Quản trị viên (Khoa CNPM)";

            // FLATTEN: mỗi thiết bị trong phiếu = 1 dòng (lặp lại thông tin header)
            var rows = requests
                .SelectMany(r => r.Items.Select(i => new List<object?>
                {
                    r.BorrowerName,
                    i.Device?.DeviceName ?? "(đã xóa)",
                    lender,
                    r.RequestDate,
                    (object?)r.ReturnDate ?? "Chưa trả",
                    i.Quantity
                } as IReadOnlyList<object?>))
                .ToList();

            var bytes = _excel.BuildWorkbook("ThietBiDaMuon", headers, rows);
            var fileName = $"ThietBiDaMuon_{statusSlug}_{filter.TimeModeSlug()}_{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
    }
}