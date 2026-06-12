using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.ViewModels;

namespace SE_Academic_Affairs_Support_System.Services.ProjectRegistration
{
    public class RegistrationPeriodStudentService : IRegistrationPeriodStudentService
    {
        private readonly AppDbContext _db;

        public RegistrationPeriodStudentService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<StudentCheckboxItem>> GetAvailableStudentsAsync(int periodId)
        {
            var allowedIds = await _db.RegistrationPeriodStudents
                .Where(rps => rps.RegistrationPeriodId == periodId)
                .Select(rps => rps.StudentProfileId)
                .ToListAsync();

            var students = await _db.StudentProfiles
                .Include(s => s.User)
                .OrderBy(s => s.StudentCode)
                .ToListAsync();

            return students.Select(s => new StudentCheckboxItem
            {
                StudentProfileId = s.Id,
                StudentCode = s.StudentCode,
                FullName = s.User?.FullName ?? s.User?.UserName ?? string.Empty,
                IsSelected = allowedIds.Contains(s.Id)
            }).ToList();
        }

        public async Task<(List<int> StudentIds, string? Error)> ParseStudentFileAsync(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var mssvList = new List<string>();

            try
            {
                if (ext == ".xlsx")
                {
                    using var stream = file.OpenReadStream();
                    using var wb = new XLWorkbook(stream);
                    var ws = wb.Worksheet(1);
                    foreach (var row in ws.RowsUsed())
                    {
                        var val = row.Cell(1).GetValue<string>()?.Trim();
                        if (!string.IsNullOrWhiteSpace(val))
                            mssvList.Add(val);
                    }
                }
                else // .txt, .csv, or anything else treated as text
                {
                    using var reader = new StreamReader(file.OpenReadStream());
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        // CSV: lấy cột đầu
                        var val = line.Split(',')[0].Trim().Trim('"');
                        if (!string.IsNullOrWhiteSpace(val))
                            mssvList.Add(val);
                    }
                }
            }
            catch (Exception ex)
            {
                return (new List<int>(), $"Lỗi đọc file: {ex.Message}");
            }

            if (mssvList.Count == 0)
                return (new List<int>(), "File không chứa MSSV nào.");

            // Bỏ dòng header nếu không phải số (heuristic)
            if (mssvList.Count > 0 && !mssvList[0].Any(char.IsDigit))
                mssvList.RemoveAt(0);

            var profiles = await _db.StudentProfiles
                .Where(s => mssvList.Contains(s.StudentCode))
                .Select(s => s.Id)
                .ToListAsync();

            var notFound = mssvList.Count - profiles.Count;
            string? warning = notFound > 0
                ? $"Có {notFound} MSSV không tìm thấy trong hệ thống và đã bỏ qua."
                : null;

            return (profiles, warning);
        }

        public async Task SaveAllowedStudentsAsync(int periodId, List<int> studentProfileIds)
        {
            var existing = await _db.RegistrationPeriodStudents
                .Where(rps => rps.RegistrationPeriodId == periodId)
                .ToListAsync();
            _db.RegistrationPeriodStudents.RemoveRange(existing);

            var newEntries = studentProfileIds.Distinct().Select(id => new RegistrationPeriodStudent
            {
                RegistrationPeriodId = periodId,
                StudentProfileId = id,
                AddedAt = DateTime.UtcNow
            });
            await _db.RegistrationPeriodStudents.AddRangeAsync(newEntries);
            await _db.SaveChangesAsync();
        }
    }
}
