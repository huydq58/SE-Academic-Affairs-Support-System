using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using AspNetCoreGeneratedDocument;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.Services.Email;
using SE_Academic_Affairs_Support_System.Services.EmailNotification;
using SE_Academic_Affairs_Support_System.ViewModels;

namespace SE_Academic_Affairs_Support_System.Controllers
{
    public class RoomController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IEmailNotificationService _emailNotif;

        public RoomController(AppDbContext context, UserManager<User> userManager,
            IEmailNotificationService emailNotif)
        {
            _context = context;
            _userManager = userManager;
            _emailNotif = emailNotif;
        }
        [AllowAnonymous]
        public async Task<IActionResult> WeeklySchedule(int roomId, DateTime? selectedDate)
        {
            DateTime currentDate = selectedDate ?? DateTime.Today;

            int diff = (7 + (currentDate.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime startOfWeek = currentDate.AddDays(-1 * diff).Date;
            DateTime endOfWeek = startOfWeek.AddDays(6).Date;

            var daysInWeek = new List<DateTime>();
            for (int i = 0; i < 7; i++)
            {
                daysInWeek.Add(startOfWeek.AddDays(i));
            }

            // Tìm chính xác phòng đó (đảm bảo nó đang hoạt động)
            var targetRoom = await _context.Rooms
                .FirstOrDefaultAsync(r => r.RoomID == roomId && r.Condition == "Good");

            if (targetRoom == null)
            {
                return NotFound("Không tìm thấy phòng này hoặc phòng đang được bảo trì!");
            }

            var rooms = new List<RoomModel> { targetRoom };

            var bookings = await _context.RoomBookings
                .Where(b => b.RoomId == roomId
                         && b.BookingDate >= startOfWeek
                         && b.BookingDate <= endOfWeek
                         && (b.Status == "Approved" ))
                .ToListAsync();

            var viewModel = new ScheduleViewModel
            {
                WeekStartDate = startOfWeek,
                WeekEndDate = endOfWeek,
                DaysInWeek = daysInWeek,
                Rooms = rooms, 

                WeeklyBookings = bookings
            };

            return View(viewModel);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CreateBooking(int roomId,DateTime date,TimeSpan startTime,TimeSpan endTime)
        {
            var room = await _context.Rooms.FindAsync(roomId);

            if (room == null)
                return NotFound();

            // Lấy user đang đăng nhập
            var user = await _userManager.GetUserAsync(User);

            var model = new CreateBookingViewModel
            {
                RoomId = roomId,
                RoomName = room.RoomName,
                BookingDate = date,
                StartTime = startTime,
                EndTime = endTime,

                // Autofill
                UserName = user.FullName,
                UserEmail = user.Email,
                PhoneNumber = user.PhoneNumber
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBooking(CreateBookingViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (model.EndTime <= model.StartTime)
            {
                ModelState.AddModelError("", "Giờ kết thúc phải lớn hơn giờ bắt đầu.");
                return View(model);
            }

            // Transaction
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

            try
            {
                // Kiểm tra trùng lịch
                bool isConflict = await _context.RoomBookings.AnyAsync(b =>
                    b.RoomId == model.RoomId &&
                    b.BookingDate.Date == model.BookingDate.Date &&
                    b.Status == "Approved" &&
                    model.StartTime < b.EndTime &&
                    model.EndTime > b.StartTime
                );

                if (isConflict)
                {
                    ModelState.AddModelError("",
                        "Khoảng thời gian này đã có người đặt.");

                    return View(model);
                }

                var booking = new RoomBooking
                {
                    RoomId = model.RoomId,
                    BookingDate = model.BookingDate,
                    UserName = model.UserName,
                    UserEmail = model.UserEmail,
                    PhoneNumber = model.PhoneNumber,
                    StartTime = model.StartTime,
                    EndTime = model.EndTime,
                    Purpose = model.Purpose,
                    Status = "Approved",
                    CreatedAt = DateTime.Now
                };

                _context.RoomBookings.Add(booking);

                await _context.SaveChangesAsync();

                // Commit transaction
                await transaction.CommitAsync();

                await _emailNotif.NotifyRoomBookingConfirmedAsync(
                    model.UserEmail ?? string.Empty, model.UserName ?? string.Empty,
                    model.RoomName ?? string.Empty,
                    model.BookingDate, model.StartTime, model.EndTime,
                    model.Purpose ?? string.Empty);

                TempData["SuccessMessage"] = "Đặt phòng thành công!";

                return RedirectToAction("WeeklySchedule",
                    new
                    {
                        roomId = model.RoomId,
                        selectedDate = model.BookingDate.ToString("yyyy-MM-dd")
                    });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError("",
                    "Có lỗi xảy ra khi đặt phòng.");

                return View(model);
            }
        }
        //[Authorize(Roles = "Admin")]
        //[HttpGet]
        //public async Task<IActionResult> PendingBookings()
        //{
        //    var pendingList = await _context.RoomBookings
        //        .Include(b => b.Room)
        //        .Where(b => b.Status == "Pending")
        //        .OrderBy(b => b.BookingDate)
        //        .ToListAsync();

        //    return View(pendingList);
        //}
        //[Authorize(Roles = "Admin")]
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> ApproveBooking(int id)
        //{
        //    // Tìm đơn đặt phòng theo ID
        //    var booking = await _context.RoomBookings.FindAsync(id);
        //    if (booking == null || booking.Status != "Pending")
        //    {
        //        return NotFound("Đơn không tồn tại hoặc đã được xử lý.");
        //    }

        //    // 1. Chuyển trạng thái đơn này thành Approved
        //    booking.Status = "Approved";


        //    var conflictingBookings = await _context.RoomBookings
        //        .Where(b => b.RoomId == booking.RoomId
        //                 && b.BookingDate.Date == booking.BookingDate.Date
        //                 && b.BookingId != booking.BookingId // Loại trừ đơn vừa duyệt
        //                 && b.Status == "Pending"

        //                 && b.StartTime < booking.EndTime
        //                 && b.EndTime > booking.StartTime)
        //        .ToListAsync();

        //    foreach (var conflict in conflictingBookings)
        //    {
        //        conflict.Status = "Rejected"; // Đổi trạng thái thành Từ chối
        //    }

        //    await _emailService.SendConfirmRoomAsync(booking.UserEmail, booking.UserName, booking.StartTime, booking.EndTime,booking.BookingDate,booking.Purpose);
        //    await _context.SaveChangesAsync();
        //    TempData["Success"] = "Đã duyệt đơn thành công! Các đơn trùng giờ đã tự động bị từ chối.";

        //    return RedirectToAction(nameof(PendingBookings)); // Hoặc thay bằng tên trang danh sách chờ duyệt của bạn
        //}
        //[Authorize(Roles = "Admin")]
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> RejectBooking(int id)
        //{
        //    var booking = await _context.RoomBookings.FindAsync(id);
        //    if (booking == null || booking.Status != "Pending")
        //    {
        //        return NotFound("Đơn không tồn tại hoặc đã được xử lý.");
        //    }

        //    booking.Status = "Rejected";


        //    await _context.SaveChangesAsync();

        //    TempData["Success"] = "Đã từ chối đơn đặt phòng.";
        //    return RedirectToAction(nameof(PendingBookings));
        //}

        [HttpGet]
        public async Task<IActionResult> GetBookingsForCalendar(int roomId)
        {
            try
            {
                var bookings = await _context.RoomBookings
                    .Where(b => b.RoomId == roomId && (b.Status == "Approved"))
                    .ToListAsync();

                var eventList = bookings.Select(b => new
                {
                    id = b.BookingId,
                    title = b.UserName,
                    purpose = b.Purpose,
                    start = b.BookingDate.Add(b.StartTime).ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = b.BookingDate.Add(b.EndTime).ToString("yyyy-MM-ddTHH:mm:ss"),
                    color = b.Status == "Approved" ? "#dc3545" : "#ffc107",
                    textColor = b.Status == "Approved" ? "#ffffff" : "#000000",
                    status = b.Status
                });

                return Json(eventList);
            }
            catch (Exception ex)
            {
                var logger = HttpContext.RequestServices.GetRequiredService<ILogger<RoomController>>();
                logger.LogError(ex, "Error loading bookings for calendar, roomId {RoomId}", roomId);
                return Json(Array.Empty<object>());
            }
        }

        // ── Admin: Quản lý / hủy lịch đặt phòng ───────────────────────────────
        // GET /Room/ManageBookings — danh sách lịch đã đặt (sắp tới) để admin xử lý.
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ManageBookings()
        {
            var today = DateTime.Today;
            var bookings = await _context.RoomBookings
                .Include(b => b.Room)
                .Where(b => b.Status == "Approved" && b.BookingDate >= today)
                .OrderBy(b => b.BookingDate).ThenBy(b => b.StartTime)
                .ToListAsync();
            return View(bookings);
        }

        // POST /Room/CancelBooking — admin hủy slot khi Khoa có việc. Gửi mail từ chối (fail-safe).
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBooking(int id, string cancelReason)
        {
            var booking = await _context.RoomBookings
                .Include(b => b.Room)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking == null) return NotFound();
            if (booking.Status == "Cancelled")
            {
                TempData["Error"] = "Lịch đặt phòng này đã được hủy trước đó.";
                return RedirectToAction(nameof(ManageBookings));
            }

            bool mailOk = await CancelBookingCoreAsync(booking, cancelReason);
            TempData["Success"] = mailOk
                ? $"Đã hủy lịch đặt phòng của {booking.UserName} và gửi email thông báo."
                : $"Đã hủy lịch của {booking.UserName}. (Gửi email thông báo thất bại — thông tin slot đã được lưu lại để xử lý.)";
            return RedirectToAction(nameof(ManageBookings));
        }

        // POST /Room/CancelBookingAjax — hủy slot ngay trên lịch (FullCalendar), trả JSON.
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBookingAjax(int id, string cancelReason)
        {
            var booking = await _context.RoomBookings
                .Include(b => b.Room)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking == null)
                return Json(new { success = false, message = "Không tìm thấy lịch đặt phòng." });
            if (booking.Status == "Cancelled")
                return Json(new { success = false, message = "Lịch này đã được hủy trước đó." });

            bool mailOk = await CancelBookingCoreAsync(booking, cancelReason);
            return Json(new
            {
                success = true,
                message = mailOk
                    ? $"Đã hủy lịch của {booking.UserName} và gửi email thông báo."
                    : $"Đã hủy lịch của {booking.UserName}. (Gửi email thất bại — đã lưu thông tin để xử lý.)"
            });
        }

        // Lõi hủy: lưu thông tin slot (commit DB trước) rồi gửi mail fail-safe. Trả về true nếu mail gửi OK.
        private async Task<bool> CancelBookingCoreAsync(RoomBooking booking, string? cancelReason)
        {
            booking.Status = "Cancelled";
            booking.CancelReason = string.IsNullOrWhiteSpace(cancelReason) ? null : cancelReason.Trim();
            booking.CancelledAt = DateTime.Now;
            booking.CancelledBy = (await _userManager.GetUserAsync(User))?.FullName ?? User.Identity?.Name;
            await _context.SaveChangesAsync();

            try
            {
                await _emailNotif.NotifyRoomBookingCancelledAsync(
                    booking.UserEmail ?? string.Empty, booking.UserName ?? string.Empty,
                    booking.Room?.RoomName ?? "phòng",
                    booking.BookingDate, booking.StartTime, booking.EndTime,
                    booking.CancelReason);
                return true;
            }
            catch (Exception ex)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<RoomController>>()
                    .LogWarning(ex, "Đã hủy đặt phòng {BookingId} nhưng gửi mail thất bại.", booking.BookingId);
                return false;
            }
        }
    }
}