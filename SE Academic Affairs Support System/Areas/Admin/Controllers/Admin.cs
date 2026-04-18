using System.Data;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SE_Academic_Affairs_Support_System.Services.ProjectRegistration;
using SE_Academic_Affairs_Support_System.ViewModels;

namespace SE_Academic_Affairs_Support_System.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    [Route("Admin/Registration/[action]/{id?}")]
    public class RegistrationController : Controller
    {
        private readonly IRegistrationService _svc;

        public RegistrationController(IRegistrationService svc)
        {
            _svc = svc;
        }

        // GET /Admin/Registration/Periods
        public async Task<IActionResult> Periods()
        {
            var periods = await _svc.GetAllPeriodsAsync();
            return View(periods);
        }

        // GET /Admin/Registration/CreatePeriod
        public IActionResult CreatePeriod() => View(new PeriodFormViewModel());

        // POST /Admin/Registration/CreatePeriod
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePeriod(PeriodFormViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            if (vm.EndDate <= vm.StartDate)
            {
                ModelState.AddModelError(nameof(vm.EndDate), "Ngày kết thúc phải sau ngày bắt đầu.");
                return View(vm);
            }

            await _svc.CreatePeriodAsync(vm);
            TempData["Success"] = "Đã tạo đợt đăng ký mới.";
            return RedirectToAction(nameof(Periods));
        }

        // GET /Admin/Registration/EditPeriod/{id}
        public async Task<IActionResult> EditPeriod(int id)
        {
            var periods = await _svc.GetAllPeriodsAsync();
            var period = periods.FirstOrDefault(p => p.Id == id);
            if (period == null) return NotFound();

            return View(new PeriodFormViewModel
            {
                Id = period.Id,
                Name = period.Name,
                CourseName = period.CourseName,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                IsActive = period.IsActive
            });
        }

        // POST /Admin/Registration/EditPeriod
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPeriod(PeriodFormViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            await _svc.UpdatePeriodAsync(vm);
            TempData["Success"] = "Đã cập nhật đợt đăng ký.";
            return RedirectToAction(nameof(Periods));
        }

        // POST /Admin/Registration/ActivatePeriod
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivatePeriod(int periodId)
        {
            await _svc.SetPeriodActiveAsync(periodId);
            TempData["Success"] = "Đã kích hoạt đợt đăng ký.";
            return RedirectToAction(nameof(Periods));
        }

        // POST /Admin/Registration/ClosePeriod
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ClosePeriod(int periodId)
        {
            await _svc.ClosePeriodAndAutoRejectPendingAsync(periodId);
            TempData["Success"] = "Đã chốt danh sách. Các đề xuất chưa duyệt đã bị hủy tự động.";
            return RedirectToAction(nameof(Periods));
        }

        // GET /Admin/Registration/Export/{periodId}
        public async Task<IActionResult> Export(int periodId)
        {
            var rows = await _svc.GetExportDataAsync(periodId);

            var sb = new StringBuilder();
            sb.AppendLine("MSSV,Họ tên SV,Tên đề tài,Giảng viên hướng dẫn,Trạng thái");
            foreach (var r in rows)
                sb.AppendLine($"\"{r.StudentCode}\",\"{r.StudentName}\",\"{r.TopicTitle}\",\"{r.LecturerName}\",\"{r.Status}\"");

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", $"danh-sach-do-an-{periodId}.csv");
        }
    }
}
