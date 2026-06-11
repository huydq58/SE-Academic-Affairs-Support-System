// Services/TopicSyncService.cs
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Helper;
using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.Services;

public class TopicSyncService : BackgroundService
{
    // Dùng IServiceScopeFactory vì BackgroundService là singleton
    // nhưng DbContext là scoped
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TopicSyncService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    public TopicSyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<TopicSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TopicSyncService started.");

        // Delay nhỏ lúc startup để app khởi động xong
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncPendingRegistrationsAsync(stoppingToken);
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task SyncPendingRegistrationsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sheets = scope.ServiceProvider.GetRequiredService<GoogleSheetsService>();

            // Lấy các bản ghi chưa sync hoặc đã lỗi (retry tối đa)
            var pending = await db.TopicRegistrations
                .Include(r => r.Period)
                .Where(r => r.SyncStatus == SyncStatus.Pending
                         || r.SyncStatus == SyncStatus.Failed)
                .ToListAsync(ct);

            if (!pending.Any())
            {
                _logger.LogDebug("No pending registrations to sync.");
                return;
            }

            _logger.LogInformation("Syncing {Count} registrations...", pending.Count);

            foreach (var reg in pending)
            {
                if (ct.IsCancellationRequested) break;
                await SyncOneAsync(reg, sheets, db, ct);
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Sync completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sync cycle.");
        }
    }

    private async Task SyncOneAsync(
        TopicRegistration reg,
        GoogleSheetsService sheets,
        AppDbContext db,
        CancellationToken ct)
    {
        try
        {
            var sheetId = GoogleSheetHelper.ExtractSheetId(reg.Period.GoogleSheetLink);
            if (sheetId == null)
            {
                reg.SyncStatus = SyncStatus.Failed;
                reg.SyncError = "Sheet link không hợp lệ.";
                return;
            }

            // Gọi Apps Script để ghi lên sheet
            var result = await sheets.RegisterAsync(new RegisterTopicRequest
            {
                SheetId = sheetId,
                RowIndex = reg.RowIndex,
                StudentName = reg.StudentName1,
                StudentId = reg.StudentId1,
                // Sinh viên 2 (nếu có)
                StudentName2 = reg.StudentName2,
                StudentId2 = reg.StudentId2
            });

            if (result.Success)
            {
                reg.SyncStatus = SyncStatus.Synced;
                reg.LastSyncedAt = DateTime.Now;
                reg.SyncError = null;
                _logger.LogInformation(
                    "Synced registration {Id} (row {Row}).", reg.Id, reg.RowIndex);
            }
            else
            {
                reg.SyncStatus = SyncStatus.Failed;
                reg.SyncError = result.Message;
                _logger.LogWarning(
                    "Sync failed for registration {Id}: {Error}", reg.Id, result.Message);
            }
        }
        catch (Exception ex)
        {
            reg.SyncStatus = SyncStatus.Failed;
            reg.SyncError = ex.Message;
            _logger.LogError(ex, "Exception syncing registration {Id}.", reg.Id);
        }
    }
}