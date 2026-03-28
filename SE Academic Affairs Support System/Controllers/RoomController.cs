using AspNetCoreGeneratedDocument;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.ViewModels;
using SE_Academic_Affairs_Support_System.Services.Email;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SE_Academic_Affairs_Support_System.Controllers
{
    public class RoomController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly EmailService _emailService;
        public RoomController(AppDbContext context)
        {
            _context = context;
            _emailService = new EmailService(_config);

        }

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
        public async Task<IActionResult> CreateBooking(int roomId, DateTime date, TimeSpan startTime, TimeSpan endTime)
        {
            var room = await _context.Rooms.FindAsync(roomId);
            if (room == null) return NotFound();

            var model = new CreateBookingViewModel
            {
                RoomId = roomId,
                RoomName = room.RoomName,
                BookingDate = date,
                StartTime = startTime,  
                EndTime = endTime      
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBooking(CreateBookingViewModel model)
        {

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.EndTime <= model.StartTime)
            {
                ModelState.AddModelError("", "Giờ kết thúc phải lớn hơn giờ bắt đầu.");
                return View(model);
            }

            bool isConflict = await _context.RoomBookings.AnyAsync(b =>
                b.RoomId == model.RoomId &&
                b.BookingDate.Date == model.BookingDate.Date &&
                b.Status == "Approved" &&
                (model.StartTime < b.EndTime && model.EndTime > b.StartTime)
            );

            if (isConflict)
            {
                ModelState.AddModelError("", "Rất tiếc! Khoảng thời gian này đã có người đặt hoặc bị trùng lấn. Vui lòng chọn giờ khác.");
                return View(model);
            }

            try
            {
                var booking = new RoomBooking
                {
                    RoomId = model.RoomId,
                    BookingDate = model.BookingDate,
                    UserName = model.UserName,
                    PhoneNumber = model.PhoneNumber,
                    StartTime = model.StartTime,
                    EndTime = model.EndTime,
                    Purpose = model.Purpose ,
                    Status = "Pending",
                    CreatedAt = DateTime.Now
                };

                _context.RoomBookings.Add(booking);
                await _context.SaveChangesAsync();


                TempData["SuccessMessage"] = "Gửi yêu cầu đặt phòng thành công! Vui lòng chờ Giáo vụ duyệt.";


                return RedirectToAction("WeeklySchedule", new { roomId = model.RoomId, selectedDate = model.BookingDate.ToString("yyyy-MM-dd") });
            }
            catch (Exception ex)
            {

                ModelState.AddModelError("", "Đã xảy ra lỗi khi lưu dữ liệu. Vui lòng thử lại sau.");
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> PendingBookings()
        {
            var pendingList = await _context.RoomBookings
                .Include(b => b.Room)
                .Where(b => b.Status == "Pending")
                .OrderBy(b => b.BookingDate)
                .ToListAsync();

            return View(pendingList);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveBooking(int id)
        {
            // Tìm đơn đặt phòng theo ID
            var booking = await _context.RoomBookings.FindAsync(id);
            if (booking == null || booking.Status != "Pending")
            {
                return NotFound("Đơn không tồn tại hoặc đã được xử lý.");
            }

            // 1. Chuyển trạng thái đơn này thành Approved
            booking.Status = "Approved";


            var conflictingBookings = await _context.RoomBookings
                .Where(b => b.RoomId == booking.RoomId
                         && b.BookingDate.Date == booking.BookingDate.Date
                         && b.BookingId != booking.BookingId // Loại trừ đơn vừa duyệt
                         && b.Status == "Pending"

                         && b.StartTime < booking.EndTime
                         && b.EndTime > booking.StartTime)
                .ToListAsync();

            foreach (var conflict in conflictingBookings)
            {
                conflict.Status = "Rejected"; // Đổi trạng thái thành Từ chối
            }

           
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã duyệt đơn thành công! Các đơn trùng giờ đã tự động bị từ chối.";

            return RedirectToAction(nameof(PendingBookings)); // Hoặc thay bằng tên trang danh sách chờ duyệt của bạn
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

        [HttpGet]
        public async Task<IActionResult> GetBookingsForCalendar(int roomId)
        {
            // Lấy các đơn đặt phòng (cả Approved và Pending)
            var bookings = await _context.RoomBookings
                .Where(b => b.RoomId == roomId && (b.Status == "Approved" ))
                .ToListAsync();

            // Chuyển đổi dữ liệu sang định dạng FullCalendar hiểu được
            var eventList = bookings.Select(b => new
            {
                id = b.BookingId,
                title = b.UserName,
                purpose = b.Purpose,
                start = b.BookingDate.Add(b.StartTime).ToString("yyyy-MM-ddTHH:mm:ss"),
                end = b.BookingDate.Add(b.EndTime).ToString("yyyy-MM-ddTHH:mm:ss"),


                // Trạng thái Approved màu đỏ, Pending màu cam/vàng
                color = b.Status == "Approved" ? "#dc3545" : "#ffc107",
                textColor = b.Status == "Approved" ? "#ffffff" : "#000000",

                status = b.Status // Truyền thêm dữ liệu phụ để dùng trên View nếu cần
            });

            return Json(eventList);
        }
    }
}