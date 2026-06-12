using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.Services.ProjectRegistration;

namespace SE_Academic_Affairs_Support_System.Services.PeriodAutoClose
{
    public class PeriodAutoCloseService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PeriodAutoCloseService> _logger;

        public PeriodAutoCloseService(IServiceScopeFactory scopeFactory, ILogger<PeriodAutoCloseService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndCloseExpiredPeriodsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PeriodAutoCloseService: lỗi khi kiểm tra đợt đăng ký hết hạn.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task CheckAndCloseExpiredPeriodsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var regSvc = scope.ServiceProvider.GetRequiredService<IRegistrationService>();

            var now = DateTime.UtcNow;

            // Tìm đợt đang active nhưng đã qua EndDate
            var expiredPeriods = await db.RegistrationPeriods
                .Where(p => p.IsActive && p.EndDate < now)
                .ToListAsync();

            foreach (var period in expiredPeriods)
            {
                _logger.LogInformation(
                    "PeriodAutoCloseService: Tự động đóng đợt đăng ký #{Id} '{Name}' (hết hạn {End:dd/MM/yyyy}).",
                    period.Id, period.Name, period.EndDate);

                await regSvc.ClosePeriodAndAutoRejectPendingAsync(period.Id);
            }
        }
    }
}
