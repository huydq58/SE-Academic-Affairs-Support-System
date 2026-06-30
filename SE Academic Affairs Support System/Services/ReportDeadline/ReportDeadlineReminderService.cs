using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.Services.EmailNotification;

namespace SE_Academic_Affairs_Support_System.Services.ReportDeadline
{
    // Nhắc SV nộp báo cáo mỗi ngày khi còn ≤ 7 ngày tới hạn (tối đa 1 lần/ngày/đợt).
    public class ReportDeadlineReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReportDeadlineReminderService> _logger;

        public ReportDeadlineReminderService(
            IServiceScopeFactory scopeFactory,
            ILogger<ReportDeadlineReminderService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ReportDeadlineReminderService tick failed");
                }

                // Kiểm tra mỗi 6 giờ; LastReminderSentDate đảm bảo chỉ gửi 1 lần/ngày.
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }

        private async Task RunOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var emailNotif = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();

            var today = DateTime.Today;
            var now = DateTime.Now;

            var periods = await db.RegistrationPeriods
                .Where(p => p.ReportDeadline != null && p.ReportDeadline > now)
                .ToListAsync(ct);

            foreach (var period in periods)
            {
                var daysLeft = (period.ReportDeadline!.Value.Date - today).Days;
                if (daysLeft > 7 || daysLeft < 0) continue;                 // chỉ nhắc khi còn ≤ 7 ngày
                if (period.LastReminderSentDate?.Date == today) continue;   // đã nhắc hôm nay

                var submittedStudentIds = await db.ReportSubmissions
                    .Where(rs => rs.RegistrationPeriodId == period.Id)
                    .Select(rs => rs.StudentProfileId)
                    .ToListAsync(ct);

                var students = await db.Registrations
                    .Where(r => r.RegistrationPeriodId == period.Id
                             && r.Status == RegistrationStatus.APPROVED
                             && !submittedStudentIds.Contains(r.StudentProfileId))
                    .Select(r => new { r.Student.User.Email, r.Student.User.FullName, r.Student.StudentCode })
                    .Distinct()
                    .ToListAsync(ct);

                foreach (var s in students)
                    if (!string.IsNullOrWhiteSpace(s.Email))
                        await emailNotif.NotifyReportDeadlineAsync(
                            s.Email!, s.FullName ?? s.StudentCode, period.Name,
                            period.ReportDeadline.Value, isReminder: true, daysLeft: daysLeft);

                period.LastReminderSentDate = today;
                await db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Đã gửi nhắc hạn nộp báo cáo cho {Count} SV (đợt {PeriodId}, còn {Days} ngày).",
                    students.Count, period.Id, daysLeft);
            }
        }
    }
}
