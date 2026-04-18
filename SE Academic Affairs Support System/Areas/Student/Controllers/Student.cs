using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.Services.ProjectRegistration;
using SE_Academic_Affairs_Support_System.ViewModels;

namespace SE_Academic_Affairs_Support_System.Areas.Student.Controllers
{
    [Authorize(Roles = "Student")]
    [Area("Student")]
    [Route("Student/Registration/[action]/{id?}")]
    public class RegistrationController : Controller
    {
        private readonly IRegistrationService _svc;
        private readonly UserManager<User> _userMgr;
        private readonly AppDbContext _db;

        public RegistrationController(
            IRegistrationService svc,
            UserManager<User> userMgr,
            AppDbContext db)
        {
            _svc = svc;
            _userMgr = userMgr;
            _db = db;
        }

        // Helper: get current student's profile id
        private async Task<int?> GetStudentProfileIdAsync()
        {
            var user = await _userMgr.GetUserAsync(User);
            if (user == null) return null;
            var profile = await _db.StudentProfiles.FirstOrDefaultAsync(s => s.UserId == user.Id);
            return profile?.Id;
        }

        // GET /Student/Registration/TopicList
        public async Task<IActionResult> TopicList(string? keyword, int? lecturerId)
        {
            var studentId = await GetStudentProfileIdAsync();
            if (studentId == null) return Forbid();

            var vm = await _svc.GetTopicListForStudentAsync(studentId.Value, keyword, lecturerId);
            if (vm == null)
            {
                TempData["Info"] = "Hiện chưa có đợt đăng ký nào đang mở.";
                return View("NoPeriod");
            }
            return View(vm);
        }

        // POST /Student/Registration/Register
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(int topicId)
        {
            var studentId = await GetStudentProfileIdAsync();
            if (studentId == null) return Forbid();

            var (success, message) = await _svc.RegisterExistingTopicAsync(studentId.Value, topicId);

            TempData[success ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(TopicList));
        }

        // GET /Student/Registration/ProposeNew
        public async Task<IActionResult> ProposeNew()
        {
            var period = await _svc.GetActivePeriodAsync();
            if (period == null)
                return View("NoPeriod");

            var lecturers = await _db.LecturerProfiles
                .Include(l => l.User)
                .Select(l => new LecturerSelectItem
                { Id = l.Id, FullName = l.User.FullName, LecturerCode = l.LecturerCode })
                .ToListAsync();

            var vm = new ProposalViewModel
            {
                RegistrationPeriodId = period.Id,
                AvailableLecturers = lecturers
            };
            return View(vm);
        }

        // POST /Student/Registration/ProposeNew
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ProposeNew(ProposalViewModel vm)
        {
            var studentId = await GetStudentProfileIdAsync();
            if (studentId == null) return Forbid();

            // Re-populate lecturers if model invalid
            if (!ModelState.IsValid)
            {
                vm.AvailableLecturers = await _db.LecturerProfiles
                    .Include(l => l.User)
                    .Select(l => new LecturerSelectItem
                    { Id = l.Id, FullName = l.User.FullName, LecturerCode = l.LecturerCode })
                    .ToListAsync();
                return View(vm);
            }

            var (success, message) = await _svc.SubmitProposalAsync(studentId.Value, vm);
            TempData[success ? "Success" : "Error"] = message;

            return success
                ? RedirectToAction(nameof(MyRegistrations))
                : RedirectToAction(nameof(ProposeNew));
        }

        // GET /Student/Registration/ReviseProposal/{id}
        public async Task<IActionResult> ReviseProposal(int id)
        {
            var studentId = await GetStudentProfileIdAsync();
            if (studentId == null) return Forbid();

            var vm = await _svc.GetProposalForRevisionAsync(id, studentId.Value);
            if (vm == null)
            {
                TempData["Error"] = "Không tìm thấy đề xuất cần chỉnh sửa.";
                return RedirectToAction(nameof(MyRegistrations));
            }
            return View(vm);
        }

        // POST /Student/Registration/ReviseProposal/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviseProposal(int id, ProposalViewModel vm)
        {
            var studentId = await GetStudentProfileIdAsync();
            if (studentId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                vm.AvailableLecturers = await _db.LecturerProfiles
                    .Include(l => l.User)
                    .Select(l => new LecturerSelectItem
                    { Id = l.Id, FullName = l.User.FullName, LecturerCode = l.LecturerCode })
                    .ToListAsync();
                return View(vm);
            }

            var (success, message) = await _svc.ResubmitProposalAsync(studentId.Value, id, vm);
            TempData[success ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(MyRegistrations));
        }

        // POST /Student/Registration/Cancel
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int registrationId)
        {
            var studentId = await GetStudentProfileIdAsync();
            if (studentId == null) return Forbid();

            var (success, message) = await _svc.CancelRegistrationAsync(studentId.Value, registrationId);
            TempData[success ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(MyRegistrations));
        }

        // GET /Student/Registration/MyRegistrations
        public async Task<IActionResult> MyRegistrations()
        {
            var studentId = await GetStudentProfileIdAsync();
            if (studentId == null) return Forbid();

            var vm = await _svc.GetMyRegistrationsAsync(studentId.Value);
            return View(vm);
        }
    }
}
