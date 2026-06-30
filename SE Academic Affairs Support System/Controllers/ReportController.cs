using System.IO.Compression;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.ViewModels;

namespace SE_Academic_Affairs_Support_System.Controllers
{
    [Authorize]
    public class ReportController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<User> _userManager;
        private readonly IWebHostEnvironment _env;

        private const long MaxBytes = 524_288_000; // 500 MB
        private static readonly string[] AllowedExt = { ".doc", ".docx", ".pdf", ".zip", ".rar" };

        public ReportController(AppDbContext db, UserManager<User> userManager, IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
        }

        private async Task<StudentProfile?> GetStudentAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;
            return await _db.StudentProfiles.Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == user.Id);
        }

        private string ReportDir(int periodId) =>
            Path.Combine(_env.ContentRootPath, "App_Data", "reports", periodId.ToString());

        // GET /Report  (landing trên navbar SV) — liệt kê các đợt SV đã được duyệt đề tài + hạn nộp + trạng thái nộp.
        public async Task<IActionResult> MyReports()
        {
            var student = await GetStudentAsync();
            if (student == null) return Forbid();

            var regs = await _db.Registrations
                .Where(r => r.StudentProfileId == student.Id && r.Status == RegistrationStatus.APPROVED)
                .Include(r => r.Topic)
                .Include(r => r.RegistrationPeriod)
                .ToListAsync();

            var periodIds = regs.Select(r => r.RegistrationPeriodId).Distinct().ToList();
            var subs = await _db.ReportSubmissions
                .Where(s => s.StudentProfileId == student.Id && periodIds.Contains(s.RegistrationPeriodId))
                .ToListAsync();

            var rows = regs
                .GroupBy(r => r.RegistrationPeriodId)
                .Select(g =>
                {
                    var r = g.First();
                    return new MyReportRow
                    {
                        PeriodId = r.RegistrationPeriodId,
                        PeriodName = r.RegistrationPeriod.Name,
                        TopicTitle = r.ProposedTitle ?? r.Topic?.Title ?? string.Empty,
                        Deadline = r.RegistrationPeriod.ReportDeadline,
                        Submission = subs.FirstOrDefault(s => s.RegistrationPeriodId == r.RegistrationPeriodId)
                    };
                })
                .OrderByDescending(x => x.Deadline ?? DateTime.MinValue)
                .ToList();

            return View(rows);
        }

        // GET /Report/Index?periodId=5
        public async Task<IActionResult> Index(int periodId)
        {
            var student = await GetStudentAsync();
            if (student == null) return Forbid();

            var period = await _db.RegistrationPeriods.FindAsync(periodId);
            if (period == null) return NotFound();

            var reg = await _db.Registrations
                .Include(r => r.Topic)
                .FirstOrDefaultAsync(r => r.RegistrationPeriodId == periodId
                    && r.StudentProfileId == student.Id
                    && r.Status == RegistrationStatus.APPROVED);

            var current = await _db.ReportSubmissions
                .FirstOrDefaultAsync(s => s.RegistrationPeriodId == periodId && s.StudentProfileId == student.Id);

            var vm = new ReportSubmissionViewModel
            {
                PeriodId = periodId,
                PeriodName = period.Name,
                Deadline = period.ReportDeadline,
                HasApprovedTopic = reg != null,
                TopicTitle = reg?.ProposedTitle ?? reg?.Topic?.Title ?? string.Empty,
                Current = current
            };
            return View(vm);
        }

        // POST /Report/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxBytes)]
        public async Task<IActionResult> Upload(int periodId, IFormFile? file)
        {
            var student = await GetStudentAsync();
            if (student == null) return Forbid();

            var period = await _db.RegistrationPeriods.FindAsync(periodId);
            if (period == null) return NotFound();

            bool approved = await _db.Registrations.AnyAsync(r => r.RegistrationPeriodId == periodId
                && r.StudentProfileId == student.Id && r.Status == RegistrationStatus.APPROVED);
            if (!approved)
            {
                TempData["Error"] = "Bạn chưa được duyệt đề tài trong đợt này nên không thể nộp báo cáo.";
                return RedirectToAction(nameof(Index), new { periodId });
            }
            if (period.ReportDeadline == null)
            {
                TempData["Error"] = "Đợt này chưa có hạn nộp báo cáo.";
                return RedirectToAction(nameof(Index), new { periodId });
            }
            if (period.ReportDeadline.Value < DateTime.Now)
            {
                TempData["Error"] = "Đã quá hạn nộp báo cáo.";
                return RedirectToAction(nameof(Index), new { periodId });
            }
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file báo cáo.";
                return RedirectToAction(nameof(Index), new { periodId });
            }
            if (file.Length > MaxBytes)
            {
                TempData["Error"] = "File vượt quá giới hạn 500MB.";
                return RedirectToAction(nameof(Index), new { periodId });
            }
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExt.Contains(ext))
            {
                TempData["Error"] = "Chỉ chấp nhận file .doc, .docx, .pdf, .zip, .rar.";
                return RedirectToAction(nameof(Index), new { periodId });
            }

            var dir = ReportDir(periodId);
            Directory.CreateDirectory(dir);
            var storedName = $"{student.Id}_{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(dir, storedName);
            using (var fs = new FileStream(fullPath, FileMode.Create))
                await file.CopyToAsync(fs);

            var existing = await _db.ReportSubmissions
                .FirstOrDefaultAsync(s => s.RegistrationPeriodId == periodId && s.StudentProfileId == student.Id);

            if (existing != null)
            {
                // Nộp lại → xóa file cũ
                try
                {
                    var old = Path.Combine(dir, existing.StoredFileName);
                    if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
                }
                catch { /* không chặn nếu xóa file cũ thất bại */ }

                existing.FileName = Path.GetFileName(file.FileName);
                existing.StoredFileName = storedName;
                existing.ContentType = file.ContentType;
                existing.FileSize = file.Length;
                existing.SubmittedAt = DateTime.Now;
            }
            else
            {
                _db.ReportSubmissions.Add(new ReportSubmission
                {
                    StudentProfileId = student.Id,
                    RegistrationPeriodId = periodId,
                    FileName = Path.GetFileName(file.FileName),
                    StoredFileName = storedName,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    SubmittedAt = DateTime.Now
                });
            }
            await _db.SaveChangesAsync();

            TempData["Success"] = "Đã nộp báo cáo thành công.";
            return RedirectToAction(nameof(Index), new { periodId });
        }

        // ── Admin: danh sách bài nộp + gom tải toàn bộ ───────────────────────
        // GET /Report/Submissions?periodId=
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Submissions(int? periodId)
        {
            // Các đợt có bài nộp (cho dropdown lọc)
            var periodCounts = await _db.ReportSubmissions
                .GroupBy(s => s.RegistrationPeriodId)
                .Select(g => new { PeriodId = g.Key, Count = g.Count() })
                .ToListAsync();

            var periodNames = await _db.RegistrationPeriods
                .Where(p => periodCounts.Select(pc => pc.PeriodId).Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();

            var query = _db.ReportSubmissions
                .Include(s => s.Student).ThenInclude(st => st!.User)
                .Include(s => s.Period)
                .AsQueryable();
            if (periodId.HasValue)
                query = query.Where(s => s.RegistrationPeriodId == periodId.Value);

            var subs = await query
                .OrderByDescending(s => s.SubmittedAt)
                .ToListAsync();

            var vm = new AdminSubmissionsViewModel
            {
                PeriodId = periodId,
                PeriodName = periodId.HasValue
                    ? periodNames.FirstOrDefault(p => p.Id == periodId.Value)?.Name
                    : null,
                Periods = periodNames.Select(p => new AdminSubmissionPeriodOption
                {
                    Id = p.Id,
                    Name = p.Name,
                    Count = periodCounts.FirstOrDefault(pc => pc.PeriodId == p.Id)?.Count ?? 0
                }).OrderBy(p => p.Name).ToList(),
                Rows = subs.Select(s => new AdminSubmissionRow
                {
                    Id = s.Id,
                    StudentCode = s.Student?.StudentCode ?? string.Empty,
                    StudentName = s.Student?.User?.FullName ?? string.Empty,
                    PeriodName = s.Period?.Name ?? string.Empty,
                    FileName = s.FileName,
                    FileSize = s.FileSize,
                    SubmittedAt = s.SubmittedAt
                }).ToList()
            };

            if (periodId.HasValue)
                vm.ApprovedCount = await _db.Registrations.CountAsync(r =>
                    r.RegistrationPeriodId == periodId.Value && r.Status == RegistrationStatus.APPROVED);

            return View(vm);
        }

        // GET /Report/DownloadAll?periodId=5 — gom tất cả bài nộp của 1 đợt thành 1 file .zip
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DownloadAll(int periodId)
        {
            var period = await _db.RegistrationPeriods.FindAsync(periodId);
            var subs = await _db.ReportSubmissions
                .Where(s => s.RegistrationPeriodId == periodId)
                .Include(s => s.Student)
                .ToListAsync();

            if (subs.Count == 0)
            {
                TempData["Error"] = "Đợt này chưa có bài nộp nào.";
                return RedirectToAction(nameof(Submissions), new { periodId });
            }

            var dir = ReportDir(periodId);
            var tempZip = Path.Combine(Path.GetTempPath(), $"reports_{periodId}_{Guid.NewGuid():N}.zip");

            using (var zipStream = new FileStream(tempZip, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in subs)
                {
                    var src = Path.Combine(dir, s.StoredFileName);
                    if (!System.IO.File.Exists(src)) continue;

                    var baseName = SanitizeFileName($"{(string.IsNullOrEmpty(s.Student?.StudentCode) ? s.StudentProfileId.ToString() : s.Student!.StudentCode)}_{s.FileName}");
                    var entryName = baseName;
                    int n = 1;
                    while (!used.Add(entryName))
                        entryName = $"{Path.GetFileNameWithoutExtension(baseName)}_{++n}{Path.GetExtension(baseName)}";

                    archive.CreateEntryFromFile(src, entryName);
                }
            }

            // DeleteOnClose: file tạm tự xóa sau khi response gửi xong
            var responseStream = new FileStream(tempZip, FileMode.Open, FileAccess.Read,
                FileShare.Read, 4096, FileOptions.DeleteOnClose);
            var zipName = SanitizeFileName($"bao-cao_{period?.Name ?? periodId.ToString()}_{DateTime.Now:yyyyMMdd-HHmmss}.zip");
            return File(responseStream, "application/zip", zipName);
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        // GET /Report/Download/{id} — chủ sở hữu hoặc Admin/Lecturer
        public async Task<IActionResult> Download(int id)
        {
            var sub = await _db.ReportSubmissions.FindAsync(id);
            if (sub == null) return NotFound();

            bool allowed = User.IsInRole("Admin") || User.IsInRole("Lecturer");
            if (!allowed)
            {
                var student = await GetStudentAsync();
                allowed = student != null && sub.StudentProfileId == student.Id;
            }
            if (!allowed) return Forbid();

            var fullPath = Path.Combine(ReportDir(sub.RegistrationPeriodId), sub.StoredFileName);
            if (!System.IO.File.Exists(fullPath))
            {
                TempData["Error"] = "File không còn tồn tại trên hệ thống.";
                return RedirectToAction(nameof(Index), new { periodId = sub.RegistrationPeriodId });
            }

            // PhysicalFile stream — không nạp toàn bộ file lớn vào RAM
            return PhysicalFile(fullPath, sub.ContentType ?? "application/octet-stream", sub.FileName);
        }
    }
}
