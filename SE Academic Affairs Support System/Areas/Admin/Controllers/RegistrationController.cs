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
        private readonly IRegistrationPeriodStudentService _studentSvc;

        public RegistrationController(IRegistrationService svc, IRegistrationPeriodStudentService studentSvc)
        {
            _svc = svc;
            _studentSvc = studentSvc;
        }

        // GET /Admin/Registration/Periods
        public async Task<IActionResult> Periods()
        {
            var periods = await _svc.GetAllPeriodsAsync();
            return View(periods);
        }

        // GET /Admin/Registration/CreatePeriod
        public async Task<IActionResult> CreatePeriod()
        {
            var vm = new PeriodFormViewModel
            {
                AvailableStudents = await _studentSvc.GetAvailableStudentsAsync(0)
            };
            return View(vm);
        }

        // POST /Admin/Registration/CreatePeriod
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePeriod(PeriodFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.AvailableStudents = await _studentSvc.GetAvailableStudentsAsync(0);
                return View(vm);
            }

            if (vm.EndDate <= vm.StartDate)
            {
                ModelState.AddModelError(nameof(vm.EndDate), "Ngày kết thúc phải sau ngày bắt đầu.");
                vm.AvailableStudents = await _studentSvc.GetAvailableStudentsAsync(0);
                return View(vm);
            }

            // Xử lý file upload sinh viên trước khi lưu period
            if (vm.StudentListFile != null && vm.StudentListFile.Length > 0)
            {
                var (parsedIds, parseError) = await _studentSvc.ParseStudentFileAsync(vm.StudentListFile);
                if (parseError != null && parsedIds.Count == 0)
                {
                    ModelState.AddModelError(nameof(vm.StudentListFile), parseError);
                    vm.AvailableStudents = await _studentSvc.GetAvailableStudentsAsync(0);
                    return View(vm);
                }
                if (parseError != null) TempData["Info"] = parseError;
                // Merge với danh sách checkbox đã chọn
                vm.SelectedStudentIds = vm.SelectedStudentIds.Union(parsedIds).Distinct().ToList();
            }

            await _svc.CreatePeriodAsync(vm);

            // Lấy period vừa tạo (mới nhất) để lưu allowed students
            var periods = await _svc.GetAllPeriodsAsync();
            var newPeriod = periods.FirstOrDefault();
            if (newPeriod != null && vm.RestrictToAllowedStudents && vm.SelectedStudentIds.Count > 0)
                await _studentSvc.SaveAllowedStudentsAsync(newPeriod.Id, vm.SelectedStudentIds);

            TempData["Success"] = "Đã tạo đợt đăng ký mới.";
            return RedirectToAction(nameof(Periods));
        }

        // GET /Admin/Registration/EditPeriod/{id}
        public async Task<IActionResult> EditPeriod(int id)
        {
            var periods = await _svc.GetAllPeriodsAsync();
            var period = periods.FirstOrDefault(p => p.Id == id);
            if (period == null) return NotFound();

            var vm = new PeriodFormViewModel
            {
                Id = period.Id,
                Name = period.Name,
                CourseName = period.CourseName,
                GoogleSheetLink = period.GoogleSheetLink,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                IsActive = period.IsActive,
                RestrictToAllowedStudents = period.RestrictToAllowedStudents,
                AvailableStudents = await _studentSvc.GetAvailableStudentsAsync(id)
            };
            // Pre-populate SelectedStudentIds từ DB
            vm.SelectedStudentIds = vm.AvailableStudents
                .Where(s => s.IsSelected)
                .Select(s => s.StudentProfileId)
                .ToList();

            return View("CreatePeriod", vm);
        }

        // POST /Admin/Registration/EditPeriod
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPeriod(PeriodFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.AvailableStudents = await _studentSvc.GetAvailableStudentsAsync(vm.Id);
                return View("CreatePeriod", vm);
            }

            // Xử lý file upload
            if (vm.StudentListFile != null && vm.StudentListFile.Length > 0)
            {
                var (parsedIds, parseError) = await _studentSvc.ParseStudentFileAsync(vm.StudentListFile);
                if (parseError != null && parsedIds.Count == 0)
                {
                    ModelState.AddModelError(nameof(vm.StudentListFile), parseError);
                    vm.AvailableStudents = await _studentSvc.GetAvailableStudentsAsync(vm.Id);
                    return View("CreatePeriod", vm);
                }
                if (parseError != null) TempData["Info"] = parseError;
                vm.SelectedStudentIds = vm.SelectedStudentIds.Union(parsedIds).Distinct().ToList();
            }

            await _svc.UpdatePeriodAsync(vm);

            if (vm.RestrictToAllowedStudents)
                await _studentSvc.SaveAllowedStudentsAsync(vm.Id, vm.SelectedStudentIds);

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
