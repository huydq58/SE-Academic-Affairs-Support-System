using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Helper;
using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.Services;

public class TopicCreateSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TopicCreateSyncService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);
    private const int MaxRetries = 5;

    public TopicCreateSyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<TopicCreateSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TopicCreateSyncService started.");
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncPendingTopicsAsync(stoppingToken);
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task SyncPendingTopicsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sheets = scope.ServiceProvider.GetRequiredService<GoogleSheetsService>();

            var pending = await db.TopicSyncRecords
                .Include(r => r.Period)
                .Where(r => (r.SyncStatus == SyncStatus.Pending || r.SyncStatus == SyncStatus.Failed)
                         && r.RetryCount < MaxRetries)
                .ToListAsync(ct);

            if (!pending.Any())
            {
                _logger.LogDebug("No pending topic sync records.");
                return;
            }

            _logger.LogInformation("Syncing {Count} topic(s) to Google Sheets...", pending.Count);

            foreach (var record in pending)
            {
                if (ct.IsCancellationRequested) break;
                await SyncOneAsync(record, sheets, db, ct);
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Topic sync completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during topic sync cycle.");
        }
    }

    private async Task SyncOneAsync(
        TopicSyncRecord record,
        GoogleSheetsService sheets,
        AppDbContext db,
        CancellationToken ct)
    {
        try
        {
            var sheetId = GoogleSheetHelper.ExtractSheetId(record.Period.GoogleSheetLink);
            if (sheetId == null)
            {
                record.SyncStatus = SyncStatus.Failed;
                record.SyncError = "Sheet link không hợp lệ hoặc đợt đăng ký chưa có Google Sheet.";
                record.RetryCount++;
                _logger.LogWarning(
                    "TopicSync {Id}: sheet link invalid for period {PeriodId}.",
                    record.Id, record.PeriodId);
                return;
            }

            var result = await sheets.AddTopicAsync(new AddTopicRequest
            {
                SheetId = sheetId,
                TopicId = record.TopicId,
                TopicTitle = record.TopicTitle,
                TopicDescription = record.TopicDescription,
                Technologies = record.Technologies,
                Requirements = record.Requirements,
                MaxStudents = record.MaxStudents,
                LecturerName = record.LecturerName,
                LecturerCode = record.LecturerCode,
                Note = record.Note
            });

            if (result.Success)
            {
                record.SyncStatus = SyncStatus.Synced;
                record.LastSyncedAt = DateTime.Now;
                record.SyncError = null;

                // Ghi lại vị trí dòng trên sheet vào Topic entity
                if (result.RowIndex.HasValue)
                {
                    var topic = await db.Topics.FindAsync(new object[] { record.TopicId }, ct);
                    if (topic != null)
                        topic.SheetRowIndex = result.RowIndex.Value;
                }

                _logger.LogInformation(
                    "TopicSync {Id} ('{Title}') synced to row {Row}.",
                    record.Id, record.TopicTitle, result.RowIndex);
            }
            else
            {
                record.SyncStatus = SyncStatus.Failed;
                record.SyncError = result.Message;
                record.RetryCount++;
                _logger.LogWarning(
                    "TopicSync {Id}: Apps Script returned error: {Error}", record.Id, result.Message);
            }
        }
        catch (Exception ex)
        {
            record.SyncStatus = SyncStatus.Failed;
            record.SyncError = ex.Message;
            record.RetryCount++;
            _logger.LogError(ex, "Exception syncing topic record {Id}.", record.Id);
        }
    }
}
