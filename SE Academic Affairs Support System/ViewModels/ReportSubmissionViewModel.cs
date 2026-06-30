using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.ViewModels
{
    public class ReportSubmissionViewModel
    {
        public int PeriodId { get; set; }
        public string PeriodName { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;
        public DateTime? Deadline { get; set; }
        public bool HasApprovedTopic { get; set; }
        public ReportSubmission? Current { get; set; }

        public bool DeadlinePassed => Deadline.HasValue && Deadline.Value < DateTime.Now;
        public bool CanSubmit => HasApprovedTopic && Deadline.HasValue && !DeadlinePassed;
    }

    // ── Admin: quản lý bài nộp báo cáo ────────────────────────────────────────
    public class AdminSubmissionPeriodOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class AdminSubmissionRow
    {
        public int Id { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string PeriodName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime SubmittedAt { get; set; }
    }

    public class AdminSubmissionsViewModel
    {
        public int? PeriodId { get; set; }
        public string? PeriodName { get; set; }
        public List<AdminSubmissionPeriodOption> Periods { get; set; } = new();
        public List<AdminSubmissionRow> Rows { get; set; } = new();
        public int ApprovedCount { get; set; }   // số SV được duyệt đề tài (nếu lọc theo đợt)
    }

    // Dòng trong trang "Nộp báo cáo" (landing) của sinh viên — mỗi đợt đã được duyệt đề tài.
    public class MyReportRow
    {
        public int PeriodId { get; set; }
        public string PeriodName { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;
        public DateTime? Deadline { get; set; }
        public ReportSubmission? Submission { get; set; }

        public bool DeadlinePassed => Deadline.HasValue && Deadline.Value < DateTime.Now;
    }
}
