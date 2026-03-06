using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SE_Academic_Affairs_Support_System.Controllers
{
    public class RoomController : Controller
    {
        private readonly AppDbContext _context;

        public RoomController(AppDbContext context)
        {
            _context = context;
        }

        // Thêm tham số int roomId vào hàm
        public async Task<IActionResult> WeeklySchedule(int roomId, DateTime? selectedDate)
        {
            // 1. Xác định tuần cần xem (Mặc định là tuần hiện tại nếu không truyền ngày)
            DateTime currentDate = selectedDate ?? DateTime.Today;

            // Tính toán ra ngày Thứ 2 của tuần đó
            int diff = (7 + (currentDate.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime startOfWeek = currentDate.AddDays(-1 * diff).Date;
            DateTime endOfWeek = startOfWeek.AddDays(6).Date;

            // Tạo danh sách 7 ngày để View dễ bề vẽ bảng
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

            var timeSlots = await _context.TimeSlots
                .OrderBy(t => t.StartTime)
                .ToListAsync();

            var bookings = await _context.RoomBookings
                .Include(b => b.User)
                .Where(b => b.RoomId == roomId
                         && b.BookingDate >= startOfWeek
                         && b.BookingDate <= endOfWeek
                         && (b.Status == "Approved" || b.Status == "Pending"))
                .ToListAsync();

            var viewModel = new ScheduleViewModel
            {
                WeekStartDate = startOfWeek,
                WeekEndDate = endOfWeek,
                DaysInWeek = daysInWeek,
                Rooms = rooms, // List này giờ chỉ có 1 phòng duy nhất
                TimeSlots = timeSlots,
                WeeklyBookings = bookings
            };

            return View(viewModel);
        }
        // 1. GET: Lấy thông tin từ URL (khi bấm vào ô màu xanh trên lịch) để đổ vào Form
        [HttpGet]
        public async Task<IActionResult> CreateBooking(int roomId, int slotId, DateTime date)
        {
            // Chặn người dùng cố tình gõ URL vào ngày Chủ Nhật
            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                TempData["Error"] = "Hệ thống không nhận đặt phòng vào Chủ Nhật.";
                return RedirectToAction("WeeklySchedule", new { roomId = roomId });
            }

            var room = await _context.Rooms.FindAsync(roomId);
            var slot = await _context.TimeSlots.FindAsync(slotId);

            if (room == null || slot == null) return NotFound("Dữ liệu không hợp lệ.");

            var model = new CreateBookingViewModel
            {
                RoomId = roomId,
                RoomName = room.RoomName,
                SlotId = slotId,
                SlotName = slot.SlotName,
                SlotTime = $"{slot.StartTime:hh\\:mm} - {slot.EndTime:hh\\:mm}",
                BookingDate = date
            };

            return View(model);
        }

        // 2. POST: Xử lý khi người dùng bấm nút "Gửi Yêu Cầu"
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBooking(CreateBookingViewModel model)
        {
            if (model.BookingDate.DayOfWeek == DayOfWeek.Sunday)
            {
                ModelState.AddModelError("", "Hệ thống không nhận đặt phòng vào Chủ Nhật.");
            }
            if (model.BookingDate.Date < DateTime.Today)
            {
                ModelState.AddModelError("", "Không thể đặt phòng cho các ngày trong quá khứ.");
                return View(model);
            }
            if (ModelState.IsValid)
            {
                // Kiểm tra xem trong lúc họ đang điền Form, có ai nhanh tay đặt mất chỗ đó không?
                bool isConflict = await _context.RoomBookings.AnyAsync(b =>
                    b.RoomId == model.RoomId &&
                    b.SlotId == model.SlotId &&
                    b.BookingDate.Date == model.BookingDate.Date &&
                    ( b.Status == "Approved"));

                if (isConflict)
                {
                    ModelState.AddModelError("", "Rất tiếc! Khung giờ này vừa có người đặt. Vui lòng chọn giờ khác.");
                    return View(model); // Trả lại form báo lỗi
                }

                // Tạo đơn mới
                var booking = new RoomBooking
                {
                    RoomId = model.RoomId,
                    SlotId = model.SlotId,
                    BookingDate = model.BookingDate,
                    Purpose = $"{model.Purpose} (SĐT: {model.PhoneNumber})",
                    UserId = 4,

                    Status = "Pending", 
                    CreatedAt = DateTime.Now
                };

                _context.RoomBookings.Add(booking);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Gửi yêu cầu đặt phòng thành công! Vui lòng chờ Giáo vụ duyệt.";
                return RedirectToAction("WeeklySchedule", new { roomId = model.RoomId, selectedDate = model.BookingDate.ToString("yyyy-MM-dd") });
            }

            return View(model);
        }


        // 1. GET: Hiển thị danh sách các đơn đang chờ duyệt
        [HttpGet]
        public async Task<IActionResult> PendingBookings()
        {
            var pendingList = await _context.RoomBookings
                .Include(b => b.Room)
                .Include(b => b.User) 
                .Where(b => b.Status == "Pending")
                .OrderBy(b => b.BookingDate)
                .ThenBy(b => b.SlotId)
                .ToListAsync();

            return View(pendingList);
        }

        // 2. POST: Xử lý DUYỆT đơn (Chấp nhận)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveBooking(int id)
        {
            // Giả sử khóa chính của bảng RoomBookings tên là Id (hoặc BookingId, bạn tự đổi lại cho khớp DB nhé)
            var booking = await _context.RoomBookings.FindAsync(id);
            if (booking == null || booking.Status != "Pending")
            {
                return NotFound("Đơn không tồn tại hoặc đã được xử lý.");
            }

            // 1. Chuyển trạng thái đơn này thành Approved
            booking.Status = "Approved";

            // 2. TÌM VÀ TỪ CHỐI TỰ ĐỘNG các đơn khác trùng ca/ngày/phòng
            var conflictingBookings = await _context.RoomBookings
                .Where(b => b.RoomId == booking.RoomId
                         && b.SlotId == booking.SlotId
                         && b.BookingDate.Date == booking.BookingDate.Date
                         && b.BookingId != booking.BookingId // Loại trừ đơn vừa duyệt
                         && b.Status == "Pending")
                .ToListAsync();

            foreach (var conflict in conflictingBookings)
            {
                conflict.Status = "Rejected"; // Đổi trạng thái thành Từ chối
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã duyệt đơn thành công! Các đơn trùng lịch đã tự động bị từ chối.";

            return RedirectToAction(nameof(PendingBookings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectBooking(int id)
        {
            var booking = await _context.RoomBookings.FindAsync(id);
            if (booking == null || booking.Status != "Pending")
            {
                return NotFound("Đơn không tồn tại hoặc đã được xử lý.");
            }

            booking.Status = "Rejected";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã từ chối đơn đặt phòng.";
            return RedirectToAction(nameof(PendingBookings));
        }
    }
}