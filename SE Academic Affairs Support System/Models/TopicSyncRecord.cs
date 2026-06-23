namespace SE_Academic_Affairs_Support_System.Models;

public class TopicSyncRecord
{
    public int Id { get; set; }

    public int TopicId { get; set; }
    public Topic Topic { get; set; } = null!;

    public int PeriodId { get; set; }
    public RegistrationPeriod Period { get; set; } = null!;

    // Denormalized: giữ lại data gốc phòng trường hợp topic bị xóa trước khi sync xong
    public string TopicTitle { get; set; } = string.Empty;
    public string TopicDescription { get; set; } = string.Empty;
    public string? Technologies { get; set; }
    public string? Requirements { get; set; }
    public int MaxStudents { get; set; }
    public string LecturerName { get; set; } = string.Empty;
    public string LecturerCode { get; set; } = string.Empty;
    public string? Note { get; set; }

    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncedAt { get; set; }
    public string? SyncError { get; set; }
    public int RetryCount { get; set; }
}
