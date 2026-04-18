using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.Services.ProjectRegistration;
using SE_Academic_Affairs_Support_System.ViewModels;

namespace SE_Academic_Affairs_Support_System.Areas.Lecturer.Controllers
{
    [Authorize(Roles = "Lecturer")]
    [Area("Lecturer")]
    [Route("Lecturer/Registration/[action]/{id?}")]
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

        private async Task<int?> GetLecturerProfileIdAsync()
        {
            var user = await _userMgr.GetUserAsync(User);
            if (user == null) return null;
            var profile = await _db.LecturerProfiles.FirstOrDefaultAsync(l => l.UserId == user.Id);
            return profile?.Id;
        }

        // GET /Lecturer/Registration/Inbox
        public async Task<IActionResult> Inbox()
        {
            var lecturerId = await GetLecturerProfileIdAsync();
            if (lecturerId == null) return Forbid();

            var vm = await _svc.GetLecturerInboxAsync(lecturerId.Value);
            return View(vm);
        }

        // GET /Lecturer/Registration/Review/{id}
        public async Task<IActionResult> Review(int id)
        {
            var lecturerId = await GetLecturerProfileIdAsync();
            if (lecturerId == null) return Forbid();

            var vm = await _svc.GetProposalForReviewAsync(id, lecturerId.Value);
            if (vm == null)
            {
                TempData["Error"] = "Không tìm thấy đề xuất.";
                return RedirectToAction(nameof(Inbox));
            }
            return View(vm);
        }

        // POST /Lecturer/Registration/Review/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(int id, ReviewDecisionViewModel vm)
        {
            var lecturerId = await GetLecturerProfileIdAsync();
            if (lecturerId == null) return Forbid();

            vm.RegistrationId = id;

            if (!ModelState.IsValid)
            {
                // Re-populate display data
                vm.Proposal = (await _svc.GetProposalForReviewAsync(id, lecturerId.Value))?.Proposal;
                return View(vm);
            }

            var (success, message) = await _svc.ProcessDecisionAsync(lecturerId.Value, vm);
            TempData[success ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(Inbox));
        }

        // GET /Lecturer/Registration/MyTopics
        public async Task<IActionResult> MyTopics()
        {
            var lecturerId = await GetLecturerProfileIdAsync();
            if (lecturerId == null) return Forbid();

            var topics = await _svc.GetLecturerTopicsAsync(lecturerId.Value);
            var period = await _svc.GetActivePeriodAsync();
            return View(new LecturerTopicsViewModel { Topics = topics, ActivePeriod = period });
        }

        // GET /Lecturer/Registration/CreateTopic
        public async Task<IActionResult> CreateTopic()
        {
            var period = await _svc.GetActivePeriodAsync();
            if (period == null)
            {
                TempData["Info"] = "Chưa có đợt đăng ký nào đang mở để tạo đề tài.";
                return RedirectToAction(nameof(MyTopics));
            }
            return View(new CreateTopicViewModel { RegistrationPeriodId = period.Id });
        }

        // POST /Lecturer/Registration/CreateTopic
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTopic(CreateTopicViewModel vm)
        {
            var lecturerId = await GetLecturerProfileIdAsync();
            if (lecturerId == null) return Forbid();

            if (!ModelState.IsValid)
                return View(vm);

            await _svc.CreateTopicAsync(vm, lecturerId.Value);
            TempData["Success"] = "Đã tạo đề tài thành công.";
            return RedirectToAction(nameof(MyTopics));
        }

        // POST /Lecturer/Registration/DeleteTopic
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTopic(int topicId)
        {
            var lecturerId = await GetLecturerProfileIdAsync();
            if (lecturerId == null) return Forbid();

            try
            {
                await _svc.DeleteTopicAsync(topicId, lecturerId.Value);
                TempData["Success"] = "Đã xóa đề tài.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(MyTopics));
        }
    }
}
