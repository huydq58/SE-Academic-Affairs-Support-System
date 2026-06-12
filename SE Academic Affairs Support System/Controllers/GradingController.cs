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

    public async Task<IActionResult> List(int periodId, string? search)
    {
        var period = await _db.RegistrationPeriods.FindAsync(periodId);
        if (period == null) return NotFound();

        var sheetId = GoogleSheetHelper.ExtractSheetId(period.GoogleSheetLink);
        if (sheetId == null)
            return BadRequest("Đợt này chưa có link Google Sheet hợp lệ.");

        // Lấy danh sách từ sheet để hiển thị (bao gồm SV chưa có trong SQL)
        var sheetRows = await _sheets.GetGradingRowsAsync(sheetId);

        // Lấy điểm đã lưu trong SQL — ưu tiên hiển thị
        var gradeRecords = await _db.GradeRecords
            .Where(g => g.PeriodId == periodId)
            .ToListAsync();

        // Merge: nếu SQL đã có điểm thì dùng SQL, còn lại dùng sheet
        var rows = sheetRows.Select(s =>
        {
            var sql = gradeRecords.FirstOrDefault(g => g.Mssv == s.Mssv);
            return new GradingSheetRow
            {
                RowIndex = s.RowIndex,
                Mssv = s.Mssv,
                StudentName = s.StudentName,
                TopicName = s.TopicName,
                Lecturer = s.Lecturer,
                Score = sql?.Score != null ? (double?)((double)sql.Score) : s.Score,
                GradedBy = sql?.GradedBy ?? s.GradedBy,
                GradedAt = sql?.GradedAt.ToString("dd/MM/yyyy HH:mm") ?? s.GradedAt,
                SyncStatus = sql?.SyncStatus   // null nếu chưa có trong SQL
            };
        }).ToList();

        // Tìm kiếm
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
            PeriodName = period.Name,
            CourseName = period.CourseName,
            SheetId = sheetId,
            PeriodId = periodId,
            Rows = rows,
            SearchQuery = search ?? string.Empty
        };

        return View(vm);
    }

    // ─── GET: /Grading/Grade?periodId=5&sheetId=xxx&mssv=yyy ─────────────
    public async Task<IActionResult> Grade(int periodId, string sheetId, string mssv)
    {
        var sheetRows = await _sheets.GetGradingRowsAsync(sheetId);
        var row = sheetRows.FirstOrDefault(r => r.Mssv == mssv);
        if (row == null) return NotFound();

        // Nếu SQL đã có điểm thì hiển thị điểm SQL
        var existing = await _db.GradeRecords
            .FirstOrDefaultAsync(g => g.PeriodId == periodId && g.Mssv == mssv);

        if (existing != null)
        {
            row.Score = (float)existing.Score;
            row.GradedBy = existing.GradedBy;
            row.GradedAt = existing.GradedAt.ToString("dd/MM/yyyy HH:mm");
        }

        var vm = new GradeFormViewModel
        {
            SheetId = sheetId,
            PeriodId = periodId,
            Row = row,
            Score = (decimal)(row.Score ?? 0)
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
        {
            // Reload form nếu lỗi validation
            return RedirectToAction(nameof(Grade),
                new { periodId, sheetId, mssv });
        }

        var period = await _db.RegistrationPeriods.FindAsync(periodId);
        if (period == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        var gradedBy = user?.FullName ?? user?.UserName ?? "Unknown";

        // Lấy thông tin SV từ sheet để lưu vào SQL
        var sheetRows = await _sheets.GetGradingRowsAsync(sheetId);
        var row = sheetRows.FirstOrDefault(r => r.Mssv == mssv);
        if (row == null) return NotFound();

        // ── Upsert vào SQL ────────────────────────────────────────────────
        var existing = await _db.GradeRecords
            .FirstOrDefaultAsync(g => g.PeriodId == periodId && g.Mssv == mssv);

        if (existing == null)
        {
            // Thêm mới
            _db.GradeRecords.Add(new GradeRecord
            {
                PeriodId = periodId,
                RowIndex = row.RowIndex,
                Mssv = mssv,
                StudentName = row.StudentName,
                TopicName = row.TopicName,
                Lecturer = row.Lecturer,
                Score = score,
                GradedBy = gradedBy,
                GradedAt = DateTime.Now,
                SyncStatus = SyncStatus.Pending,
            });
        }
        else
        {
            // Cập nhật — đánh dấu Pending để sync lại
            existing.Score = score;
            existing.GradedBy = gradedBy;
            existing.GradedAt = DateTime.Now;
            existing.SyncStatus = SyncStatus.Pending;
            existing.SyncError = null;
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã lưu điểm {score:0.#} cho {row.StudentName}. Sẽ đồng bộ lên Google Sheet trong 1 phút.";

        return RedirectToAction(nameof(List), new { periodId });
    }
}