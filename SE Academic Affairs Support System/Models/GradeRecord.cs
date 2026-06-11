namespace SE_Academic_Affairs_Support_System.Models;

public class GradeRecord
{
    public int Id { get; set; }

    public int PeriodId { get; set; }
    public RegistrationPeriod Period { get; set; } = null!;

    // Vị trí dòng trên sheet BangDiem
    public int RowIndex { get; set; }

    public string Mssv { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string TopicName { get; set; } = string.Empty;
    public string Lecturer { get; set; } = string.Empty;

    public decimal Score { get; set; }
    public string GradedBy { get; set; } = string.Empty;
    public DateTime GradedAt { get; set; } = DateTime.Now;

    // Trạng thái sync — giống TopicRegistration
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
    public DateTime? LastSyncedAt { get; set; }
    public string? SyncError { get; set; }
}