// Controllers/TopicController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Helper;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.ViewModels;

public class TopicController : Controller
{
    private readonly GoogleSheetsService _sheets;
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<TopicController> _logger;

    public TopicController(
        GoogleSheetsService sheets,
        AppDbContext db,
        UserManager<User> userManager,
        ILogger<TopicController> logger)
    {
        _sheets = sheets;
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    // ─── GET: /Topic/List?periodId=5 ─────────────────────────────────────
    public async Task<IActionResult> List(int periodId)
    {
        var period = await _db.RegistrationPeriods.FindAsync(periodId);
        if (period == null) return NotFound();

        var sheetId = GoogleSheetHelper.ExtractSheetId(period.GoogleSheetLink);
        if (sheetId == null)
            return BadRequest("Đợt này chưa có link Google Sheet hợp lệ.");

        // Lấy danh sách đề tài từ Google Sheet
        List<TopicSheet> topics;
        try
        {
            topics = await _sheets.GetTopicsAsync(sheetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Sheets call failed in Topic.List for sheetId {SheetId}", sheetId);
            TempData["Error"] = "Không thể tải danh sách đề tài từ Google Sheet. Vui lòng thử lại sau.";
            return RedirectToAction("Index", "Home");
        }

        // Lấy các đăng ký trong đợt này từ SQL để hiển thị trạng thái chính xác
        var registrations = await _db.TopicRegistrations
            .Where(r => r.PeriodId == periodId)
            .ToListAsync();

        var vm = new TopicListSheetViewModel
        {
            PeriodName = period.Name,
            CourseName = period.CourseName,
            SheetId = sheetId,
            PeriodId = periodId,
            Topics = topics,
            Registrations = registrations   // dùng để badge trạng thái trên view
        };

        return View(vm);
    }

    // ─── POST: /Topic/Register ────────────────────────────────────────────
    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        string sheetId,
        int periodId,
        string periodName,
        string courseName,
        int rowIndex,
        string topicName,
        // Sinh viên 2 (tuỳ chọn, có thể null)
        string? partnerId)
    {
        var user = await _userManager.GetUserAsync(User);
        var studentId1 = user!.Id;
        var studentName1 = user.FullName ?? user.UserName!;

        // ── 1. Kiểm tra sinh viên 1 đã đăng ký chưa ──────────────────────
        var alreadyRegistered = await _db.TopicRegistrations
            .FirstOrDefaultAsync(r =>
                r.PeriodId == periodId &&
                (r.StudentId1 == studentId1 || r.StudentId2 == studentId1));

        if (alreadyRegistered != null)
        {
            return await ReturnListView(sheetId, periodId, periodName, courseName,
                $"Bạn đã đăng ký đề tài \"{alreadyRegistered.TopicName}\", " +
                "không thể đăng ký thêm.", isSuccess: false);
        }

        // ── 2. Kiểm tra đề tài đã có người đăng ký chưa ──────────────────
        var topicTaken = await _db.TopicRegistrations
            .AnyAsync(r => r.PeriodId == periodId && r.RowIndex == rowIndex);

        if (topicTaken)
        {
            return await ReturnListView(sheetId, periodId, periodName, courseName,
                "Đề tài này đã được đăng ký bởi sinh viên khác.", isSuccess: false);
        }

        // ── 3. Xử lý sinh viên 2 (nếu có) ───────────────────────────────
        // partnerId ở đây là MSSV (StudentCode) được nhập từ modal
        string? partnerUserId = null;
        string? studentName2 = null;

        if (!string.IsNullOrEmpty(partnerId))
        {
            // Tra cứu sinh viên 2 bằng MSSV qua StudentProfile
            var partnerProfile = await _db.StudentProfiles
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StudentCode == partnerId);

            if (partnerProfile == null)
            {
                return await ReturnListView(sheetId, periodId, periodName, courseName,
                    $"Không tìm thấy sinh viên có MSSV \"{partnerId}\" trong hệ thống.",
                    isSuccess: false);
            }

            partnerUserId = partnerProfile.UserId;

            // Không được tự ghép nhóm với chính mình
            if (partnerUserId == studentId1)
            {
                return await ReturnListView(sheetId, periodId, periodName, courseName,
                    "Không thể tự ghép nhóm với chính mình.", isSuccess: false);
            }

            // Kiểm tra partner đã đăng ký đề tài khác trong kỳ này chưa
            var partnerTaken = await _db.TopicRegistrations
                .AnyAsync(r => r.PeriodId == periodId &&
                    (r.StudentId1 == partnerUserId || r.StudentId2 == partnerUserId));

            if (partnerTaken)
            {
                return await ReturnListView(sheetId, periodId, periodName, courseName,
                    "Sinh viên ghép nhóm đã đăng ký đề tài khác trong đợt này.",
                    isSuccess: false);
            }

            studentName2 = partnerProfile.User?.FullName ?? partnerProfile.User?.UserName;
        }

        // ── 4. Ghi vào SQL (nguồn sự thật), đánh dấu Pending sync ────────
        var registration = new TopicRegistration
        {
            PeriodId = periodId,
            RowIndex = rowIndex,
            TopicName = topicName,
            StudentId1 = studentId1,
            StudentName1 = studentName1,
            StudentId2 = partnerUserId,   // lưu userId (GUID), không phải MSSV
            StudentName2 = studentName2,
            SyncStatus = SyncStatus.Pending,
            RegisteredAt = DateTime.Now
        };

        _db.TopicRegistrations.Add(registration);
        await _db.SaveChangesAsync();

        return await ReturnListView(sheetId, periodId, periodName, courseName,
            $"Đăng ký đề tài \"{topicName}\" thành công! " +
            "Dữ liệu sẽ được đồng bộ lên Google Sheet trong vài phút.",
            isSuccess: true);
    }

    // ─── Helper: load lại view sau khi xử lý ────────────────────────────
    private async Task<IActionResult> ReturnListView(
        string sheetId, int periodId, string periodName, string courseName,
        string statusMessage, bool isSuccess)
    {
        List<TopicSheet> topics;
        try
        {
            topics = await _sheets.GetTopicsAsync(sheetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Sheets call failed in Topic.ReturnListView for sheetId {SheetId}", sheetId);
            TempData["Error"] = "Không thể tải danh sách đề tài từ Google Sheet. Vui lòng thử lại sau.";
            return RedirectToAction("Index", "Home");
        }
        var registrations = await _db.TopicRegistrations
            .Where(r => r.PeriodId == periodId)
            .ToListAsync();

        var vm = new TopicListSheetViewModel
        {
            PeriodName = periodName,
            CourseName = courseName,
            SheetId = sheetId,
            PeriodId = periodId,
            Topics = topics,
            Registrations = registrations,
            StatusMessage = statusMessage,
            IsSuccess = isSuccess
        };

        return View("List", vm);
    }
}