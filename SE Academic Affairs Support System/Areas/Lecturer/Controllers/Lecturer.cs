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
        public async Task<IActionResult> MyTopics(int? periodId)
        {
            var lecturerId = await GetLecturerProfileIdAsync();
            if (lecturerId == null) return Forbid();

            var topics = await _svc.GetLecturerTopicsAsync(lecturerId.Value, periodId);
            var activePeriods = await _svc.GetActivePeriodsAsync();
            var allPeriods = await _svc.GetAllPeriodsAsync();

            // Lấy danh sách các đợt mà GV đã có đề tài
            var topicPeriodIds = topics.Select(t => t.PeriodId).ToHashSet();
            var lecturerPeriods = allPeriods.Where(p => topicPeriodIds.Contains(p.Id)).ToList();

            int pendingCount = await _db.Registrations
                .CountAsync(r => r.LecturerProfileId == lecturerId.Value
                              && r.Status == RegistrationStatus.PENDING);

            return View(new LecturerTopicsViewModel
            {
                Topics = topics,
                ActivePeriod = activePeriods.FirstOrDefault(),
                ActivePeriods = activePeriods,
                LecturerPeriods = lecturerPeriods,
                FilterPeriodId = periodId,
                PendingProposalsCount = pendingCount
            });
        }

        // GET /Lecturer/Registration/CreateTopic
        public async Task<IActionResult> CreateTopic()
        {
            var activePeriods = await _svc.GetActivePeriodsAsync();
            if (!activePeriods.Any())
            {
                TempData["Info"] = "Chưa có đợt đăng ký nào đang mở để tạo đề tài.";
                return RedirectToAction(nameof(MyTopics));
            }

            var vm = new CreateTopicViewModel
            {
                RegistrationPeriodId = activePeriods.First().Id,
                AvailablePeriods = activePeriods.Select(p => new PeriodSelectItem
                {
                    Id = p.Id,
                    Name = p.Name,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate
                }).ToList()
            };
            return View(vm);
        }

        // POST /Lecturer/Registration/CreateTopic
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTopic(CreateTopicViewModel vm)
        {
            var lecturerId = await GetLecturerProfileIdAsync();
            if (lecturerId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                var activePeriods = await _svc.GetActivePeriodsAsync();
                vm.AvailablePeriods = activePeriods.Select(p => new PeriodSelectItem
                {
                    Id = p.Id,
                    Name = p.Name,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate
                }).ToList();
                return View(vm);
            }

            await _svc.CreateTopicAsync(vm, lecturerId.Value);
            TempData["Success"] = "Đã tạo đề tài thành công. Đề tài sẽ được đồng bộ lên Google Sheets trong vài phút.";
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
        public async Task<IActionResult> ActivePeriods()
        {
            var now = DateTime.Now;

            // Lọc các đợt đăng ký đang trong thời gian mở và được kích hoạt
            var activePeriods = await _db.RegistrationPeriods
                .Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now)
                .OrderByDescending(p => p.EndDate) // Đợt nào sắp hết hạn hiện lên đầu
                .ToListAsync();

            return View(activePeriods);
        }
    }
}

