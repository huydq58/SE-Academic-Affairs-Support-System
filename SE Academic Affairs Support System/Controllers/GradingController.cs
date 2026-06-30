// Controllers/GradingController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.ViewModels;

[Authorize(Roles = "Lecturer,Admin")]
public class GradingController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<GradingController> _logger;

    public GradingController(
        AppDbContext db,
        UserManager<User> userManager,
        ILogger<GradingController> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    // Admin chấm tất cả; Lecturer chỉ chấm đề tài của chính mình.
    // Trả null = không lọc (admin); giá trị = LecturerProfileId; -1 = không khớp ai.
    private async Task<int?> GetLecturerFilterAsync()
    {
        if (User.IsInRole("Admin")) return null;
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return -1;
        var lec = await _db.LecturerProfiles.FirstOrDefaultAsync(l => l.UserId == user.Id);
        return lec?.Id ?? -1;
    }

    // Nguồn dữ liệu: các Registration đã APPROVED trong đợt (SQL là nguồn sự thật).
    private IQueryable<Registration> ApprovedRegistrations(int periodId, int? lecturerFilter)
    {
        var q = _db.Registrations
            .Where(r => r.RegistrationPeriodId == periodId && r.Status == RegistrationStatus.APPROVED)
            .Include(r => r.Student).ThenInclude(s => s.User)
            .Include(r => r.Topic)
            .Include(r => r.Lecturer).ThenInclude(l => l.User)
            .AsQueryable();
        if (lecturerFilter.HasValue)
            q = q.Where(r => r.LecturerProfileId == lecturerFilter.Value);
        return q;
    }

    private static GradingSheetRow MapRow(Registration r) => new()
    {
        RowIndex = r.Topic.SheetRowIndex ?? 0,
        Mssv = r.Student.StudentCode,
        StudentName = r.Student.User.FullName,
        TopicName = r.ProposedTitle ?? r.Topic.Title,
        Lecturer = r.Lecturer.User.FullName
    };

    public async Task<IActionResult> List(int periodId, string? search)
    {
        var period = await _db.RegistrationPeriods.FindAsync(periodId);
        if (period == null) return NotFound();

        var lecturerFilter = await GetLecturerFilterAsync();

        // Chỉ lấy những đề tài có sinh viên đã được duyệt
        var regs = await ApprovedRegistrations(periodId, lecturerFilter).ToListAsync();

        var gradeRecords = await _db.GradeRecords
            .Where(g => g.PeriodId == periodId)
            .ToListAsync();

        var rows = regs.Select(r =>
        {
            var row = MapRow(r);
            var sql = gradeRecords.FirstOrDefault(g => g.Mssv == row.Mssv);
            if (sql != null)
            {
                row.Score = (double)sql.Score;
                row.GradedBy = sql.GradedBy;
                row.GradedAt = sql.GradedAt.ToString("dd/MM/yyyy HH:mm");
                row.SyncStatus = sql.SyncStatus;
            }
            return row;
        }).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var qy = search.Trim().ToLower();
            rows = rows.Where(r =>
                r.Mssv.ToLower().Contains(qy) ||
                r.TopicName.ToLower().Contains(qy) ||
                r.StudentName.ToLower().Contains(qy)
            ).ToList();
        }

        var vm = new GradingListViewModel
        {
            PeriodName = period.Name,
            CourseName = period.CourseName,
            SheetId = string.Empty,
            PeriodId = periodId,
            Rows = rows.OrderBy(r => r.StudentName).ToList(),
            SearchQuery = search ?? string.Empty
        };

        return View(vm);
    }

    // ─── GET: /Grading/Grade?periodId=5&mssv=yyy ──────────────────────────
    public async Task<IActionResult> Grade(int periodId, string? sheetId, string mssv)
    {
        var lecturerFilter = await GetLecturerFilterAsync();

        var reg = await ApprovedRegistrations(periodId, lecturerFilter)
            .FirstOrDefaultAsync(r => r.Student.StudentCode == mssv);
        if (reg == null) return NotFound();

        var row = MapRow(reg);

        var existing = await _db.GradeRecords
            .FirstOrDefaultAsync(g => g.PeriodId == periodId && g.Mssv == mssv);
        if (existing != null)
        {
            row.Score = (double)existing.Score;
            row.GradedBy = existing.GradedBy;
            row.GradedAt = existing.GradedAt.ToString("dd/MM/yyyy HH:mm");
            row.SyncStatus = existing.SyncStatus;
        }

        var vm = new GradeFormViewModel
        {
            SheetId = sheetId ?? string.Empty,
            PeriodId = periodId,
            Row = row,
            Score = (decimal)(row.Score ?? 0)
        };

        return View(vm);
    }

    // ─── POST: /Grading/Grade ─────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Grade(int periodId, string? sheetId, string mssv, decimal score)
    {
        if (score < 0 || score > 10)
            ModelState.AddModelError("Score", "Điểm phải trong khoảng 0 – 10.");

        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Grade), new { periodId, mssv });

        var lecturerFilter = await GetLecturerFilterAsync();

        var reg = await ApprovedRegistrations(periodId, lecturerFilter)
            .FirstOrDefaultAsync(r => r.Student.StudentCode == mssv);
        if (reg == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        var gradedBy = user?.FullName ?? user?.UserName ?? "Unknown";

        var existing = await _db.GradeRecords
            .FirstOrDefaultAsync(g => g.PeriodId == periodId && g.Mssv == mssv);

        if (existing == null)
        {
            _db.GradeRecords.Add(new GradeRecord
            {
                PeriodId = periodId,
                RowIndex = reg.Topic.SheetRowIndex ?? 0,
                Mssv = mssv,
                StudentName = reg.Student.User.FullName,
                TopicName = reg.ProposedTitle ?? reg.Topic.Title,
                Lecturer = reg.Lecturer.User.FullName,
                Score = score,
                GradedBy = gradedBy,
                GradedAt = DateTime.Now,
                SyncStatus = SyncStatus.Pending,
            });
        }
        else
        {
            existing.Score = score;
            existing.GradedBy = gradedBy;
            existing.GradedAt = DateTime.Now;
            existing.SyncStatus = SyncStatus.Pending;
            existing.SyncError = null;
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã lưu điểm {score:0.#} cho {reg.Student.User.FullName}. Sẽ đồng bộ lên Google Sheet trong ít phút.";
        return RedirectToAction(nameof(List), new { periodId });
    }
}
