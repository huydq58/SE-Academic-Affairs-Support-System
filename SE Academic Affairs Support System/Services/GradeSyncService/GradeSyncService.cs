// Services/GradeSyncService.cs
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Helper;
using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.Services;

public class GradeSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GradeSyncService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    public GradeSyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<GradeSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GradeSyncService started.");
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncPendingGradesAsync(stoppingToken);
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task SyncPendingGradesAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sheets = scope.ServiceProvider.GetRequiredService<GoogleSheetsService>();

            var pending = await db.GradeRecords
                .Include(g => g.Period)
                .Where(g => g.SyncStatus == SyncStatus.Pending
                         || g.SyncStatus == SyncStatus.Failed)
                .ToListAsync(ct);

            if (!pending.Any()) return;

            _logger.LogInformation("Syncing {Count} grade records...", pending.Count);

            foreach (var grade in pending)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var sheetId = GoogleSheetHelper.ExtractSheetId(grade.Period.GoogleSheetLink);
                    if (sheetId == null)
                    {
                        grade.SyncStatus = SyncStatus.Failed;
                        grade.SyncError = "Sheet link không hợp lệ.";
                        continue;
                    }

                    var result = await sheets.GradeAsync(new GradeTopicRequest
                    {
                        SheetId = sheetId,
                        Mssv = grade.Mssv,
                        Score = (float)grade.Score,
                        GradedBy = grade.GradedBy
                    });

                    _logger.LogInformation(
                        "GradeSync {Id}: Success={S}, Message={M}",
                        grade.Id, result.Success, result.Message);

                    if (result.Success)
                    {
                        grade.SyncStatus = SyncStatus.Synced;
                        grade.LastSyncedAt = DateTime.Now;
                        grade.SyncError = null;
                    }
                    else
                    {
                        grade.SyncStatus = SyncStatus.Failed;
                        grade.SyncError = result.Message;
                    }
                }
                catch (Exception ex)
                {
                    grade.SyncStatus = SyncStatus.Failed;
                    grade.SyncError = ex.Message;
                    _logger.LogError(ex, "Exception syncing grade {Id}.", grade.Id);
                }
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Grade sync completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during grade sync cycle.");
        }
    }
}