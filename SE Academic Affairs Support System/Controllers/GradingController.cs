// Controllers/GradingController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Helper;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.ViewModels;

[Authorize(Roles = "Lecturer,Admin")]
public class GradingController : Controller
{
    private readonly GoogleSheetsService _sheets;
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;

    public GradingController(
        GoogleSheetsService sheets,
        AppDbContext db,
        UserManager<User> userManager)
    {
        _sheets = sheets;
        _db = db;
        _userManager = userManager;
    }

    // Lấy danh sách dòng đề tài có SV từ DanhSachDeTai, map sang GradingSheetRow
    private static List<GradingSheetRow> MapTopicsToGradingRows(List<TopicSheet> topics)
        => topics
            .Where(t => !string.IsNullOrWhiteSpace(t.Mssv1))
            .Select(t => new GradingSheetRow
            {
                RowIndex    = t.RowIndex,
                Mssv        = t.Mssv1,
                StudentName = t.Student1,
                TopicName   = t.TopicName,
                Lecturer    = t.Lecturer
            }).ToList();

    public async Task<IActionResult> List(int periodId, string? search)
    {
        var period = await _db.RegistrationPeriods.FindAsync(periodId);
        if (period == null) return NotFound();

        var sheetId = GoogleSheetHelper.ExtractSheetId(period.GoogleSheetLink);
        if (sheetId == null)
            return BadRequest("Đợt này chưa có link Google Sheet hợp lệ.");

        // Load từ DanhSachDeTai — lọc các dòng đã có SV đăng ký
        var sheetTopics = await _sheets.GetTopicsAsync(sheetId);
        var sheetRows   = MapTopicsToGradingRows(sheetTopics);

        // Merge với điểm đã lưu trong SQL
        var gradeRecords = await _db.GradeRecords
            .Where(g => g.PeriodId == periodId)
            .ToListAsync();

        var rows = sheetRows.Select(s =>
        {
            var sql = gradeRecords.FirstOrDefault(g => g.Mssv == s.Mssv);
            return new GradingSheetRow
            {
                RowIndex    = s.RowIndex,
                Mssv        = s.Mssv,
                StudentName = s.StudentName,
                TopicName   = s.TopicName,
                Lecturer    = s.Lecturer,
                Score       = sql?.Score != null ? (double?)((double)sql.Score) : null,
                GradedBy    = sql?.GradedBy ?? string.Empty,
                GradedAt    = sql?.GradedAt.ToString("dd/MM/yyyy HH:mm") ?? string.Empty,
                SyncStatus  = sql?.SyncStatus
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            rows = rows.Where(r =>
                r.Mssv.ToLower().Contains(q) ||
                r.TopicName.ToLower().Contains(q) ||
                r.StudentName.ToLower().Contains(q)
            ).ToList();
        }

        var vm = new GradingListViewModel
        {
            PeriodName  = period.Name,
            CourseName  = period.CourseName,
            SheetId     = sheetId,
            PeriodId    = periodId,
            Rows        = rows,
            SearchQuery = search ?? string.Empty
        };

        return View(vm);
    }

    // ─── GET: /Grading/Grade?periodId=5&sheetId=xxx&mssv=yyy ─────────────
    public async Task<IActionResult> Grade(int periodId, string sheetId, string mssv)
    {
        // Tìm dòng SV trong DanhSachDeTai
        var sheetTopics = await _sheets.GetTopicsAsync(sheetId);
        var topicRow    = sheetTopics.FirstOrDefault(t => t.Mssv1 == mssv);
        if (topicRow == null) return NotFound();

        var row = new GradingSheetRow
        {
            RowIndex    = topicRow.RowIndex,
            Mssv        = topicRow.Mssv1,
            StudentName = topicRow.Student1,
            TopicName   = topicRow.TopicName,
            Lecturer    = topicRow.Lecturer
        };

        // Nếu SQL đã có điểm thì hiển thị điểm SQL
        var existing = await _db.GradeRecords
            .FirstOrDefaultAsync(g => g.PeriodId == periodId && g.Mssv == mssv);

        if (existing != null)
        {
            row.Score      = (double)existing.Score;
            row.GradedBy   = existing.GradedBy;
            row.GradedAt   = existing.GradedAt.ToString("dd/MM/yyyy HH:mm");
            row.SyncStatus = existing.SyncStatus;
        }

        var vm = new GradeFormViewModel
        {
            SheetId  = sheetId,
            PeriodId = periodId,
            Row      = row,
            Score    = (decimal)(row.Score ?? 0)
        };

        return View(vm);
    }

    // ─── POST: /Grading/Grade ─────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Grade(
        int periodId,
        string sheetId,
        string mssv,
        decimal score)
    {
        if (score < 0 || score > 10)
            ModelState.AddModelError("Score", "Điểm phải trong khoảng 0 – 10.");

        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Grade), new { periodId, sheetId, mssv });

        var period = await _db.RegistrationPeriods.FindAsync(periodId);
        if (period == null) return NotFound();

        var user     = await _userManager.GetUserAsync(User);
        var gradedBy = user?.FullName ?? user?.UserName ?? "Unknown";

        // Lấy thông tin SV từ DanhSachDeTai
        var sheetTopics = await _sheets.GetTopicsAsync(sheetId);
        var topicRow    = sheetTopics.FirstOrDefault(t => t.Mssv1 == mssv);
        if (topicRow == null) return NotFound();

        // ── Upsert vào SQL ────────────────────────────────────────────────
        var existing = await _db.GradeRecords
            .FirstOrDefaultAsync(g => g.PeriodId == periodId && g.Mssv == mssv);

        if (existing == null)
        {
            _db.GradeRecords.Add(new GradeRecord
            {
                PeriodId    = periodId,
                RowIndex    = topicRow.RowIndex,
                Mssv        = mssv,
                StudentName = topicRow.Student1,
                TopicName   = topicRow.TopicName,
                Lecturer    = topicRow.Lecturer,
                Score       = score,
                GradedBy    = gradedBy,
                GradedAt    = DateTime.Now,
                SyncStatus  = SyncStatus.Pending,
            });
        }
        else
        {
            existing.Score      = score;
            existing.GradedBy   = gradedBy;
            existing.GradedAt   = DateTime.Now;
            existing.SyncStatus = SyncStatus.Pending;
            existing.SyncError  = null;
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã lưu điểm {score:0.#} cho {topicRow.Student1}. Sẽ đồng bộ lên Google Sheet trong 1 phút.";

        return RedirectToAction(nameof(List), new { periodId });
    }
}
