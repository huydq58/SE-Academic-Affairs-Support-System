// Models/TopicRegistration.cs
namespace SE_Academic_Affairs_Support_System.Models;

public class TopicRegistration
{
    public int Id { get; set; }

    // Khoá ngoại đến RegistrationPeriod
    public int PeriodId { get; set; }
    public RegistrationPeriod Period { get; set; } = null!;

    // Vị trí dòng trên Google Sheet (để biết đề tài nào)
    public int RowIndex { get; set; }
    public string TopicName { get; set; } = string.Empty;

    // Sinh viên 1 (bắt buộc)
    public string StudentId1 { get; set; } = string.Empty;
    public string StudentName1 { get; set; } = string.Empty;

    // Sinh viên 2 (tuỳ chọn)
    public string? StudentId2 { get; set; }
    public string? StudentName2 { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.Now;

    // Trạng thái đồng bộ lên Google Sheet
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
    public DateTime? LastSyncedAt { get; set; }
    public string? SyncError { get; set; }
}

public enum SyncStatus
{
    Pending,   // Chờ sync
    Synced,    // Đã sync thành công
    Failed     // Sync lỗi (sẽ retry)
}