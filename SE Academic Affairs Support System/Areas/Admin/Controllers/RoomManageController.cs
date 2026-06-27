using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    [Route("Admin/RoomManage/[action]/{id?}")]
    public class RoomManageController : Controller
    {
        private readonly AppDbContext _db;

        public RoomManageController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var rooms = await _db.Rooms.OrderBy(r => r.RoomName).ToListAsync();
            return View(rooms);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string roomName, string condition)
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                TempData["Error"] = "Tên phòng không được để trống.";
                return RedirectToAction(nameof(Index));
            }

            if (!RoomModel.Conditions.Contains(condition))
                condition = RoomModel.Conditions[0];

            _db.Rooms.Add(new RoomModel
            {
                RoomName = roomName.Trim(),
                Condition = condition
            });
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Đã thêm phòng \"{roomName.Trim()}\" thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCondition(int id, string condition)
        {
            var room = await _db.Rooms.FindAsync(id);
            if (room == null)
            {
                TempData["Error"] = "Không tìm thấy phòng.";
                return RedirectToAction(nameof(Index));
            }

            if (!RoomModel.Conditions.Contains(condition))
                condition = RoomModel.Conditions[0];

            room.Condition = condition;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Đã cập nhật tình trạng phòng \"{room.RoomName}\" thành \"{condition}\".";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var room = await _db.Rooms.FindAsync(id);
            if (room == null)
            {
                TempData["Error"] = "Không tìm thấy phòng.";
                return RedirectToAction(nameof(Index));
            }

            bool hasBookings = await _db.RoomBookings.AnyAsync(b => b.RoomId == id);
            if (hasBookings)
            {
                TempData["Error"] = $"Không thể xóa phòng \"{room.RoomName}\" vì đang có lịch đặt phòng liên quan.";
                return RedirectToAction(nameof(Index));
            }

            _db.Rooms.Remove(room);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Đã xóa phòng \"{room.RoomName}\".";
            return RedirectToAction(nameof(Index));
        }
    }
}
