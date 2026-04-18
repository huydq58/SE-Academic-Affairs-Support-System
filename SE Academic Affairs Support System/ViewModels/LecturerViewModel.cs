using System.ComponentModel.DataAnnotations;
using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.ViewModels
{
    // ── Lecturer: Pending Proposals ───────────────────────────────────────────
    public class LecturerInboxViewModel
    {
        public List<ProposalReviewItem> PendingProposals { get; set; } = new();
        public List<ProposalReviewItem> RecentlyActioned { get; set; } = new();
        public int TotalApprovedCount { get; set; }
        public int MaxStudentsAllowed { get; set; }
    }

    public class ProposalReviewItem
    {
        public int RegistrationId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentCode { get; set; } = string.Empty;
        public string ProposedTitle { get; set; } = string.Empty;
        public string ProposedDescription { get; set; } = string.Empty;
        public string? ProposedTechnologies { get; set; }
        public RegistrationStatus Status { get; set; }
        public int RevisionCount { get; set; }
        public string PeriodName { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ReviewDecisionViewModel
    {
        public int RegistrationId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn quyết định")]
        public string Decision { get; set; } = string.Empty; // "approve" | "revise" | "reject"

        [MaxLength(1000)]
        [Display(Name = "Ghi chú / Yêu cầu chỉnh sửa")]
        public string? Note { get; set; }

        // Read-only display
        public ProposalReviewItem? Proposal { get; set; }
    }

    // ── Lecturer: Manage Own Topics ───────────────────────────────────────────
    public class LecturerTopicsViewModel
    {
        public List<TopicManageRow> Topics { get; set; } = new();
        public RegistrationPeriod? ActivePeriod { get; set; }
    }

    public class TopicManageRow
    {
        public int TopicId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int MaxStudents { get; set; }
        public int RegisteredCount { get; set; }
        public TopicStatus Status { get; set; }
        public string PeriodName { get; set; } = string.Empty;
    }

    public class CreateTopicViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tiêu đề đề tài")]
        [MaxLength(300)]
        [Display(Name = "Tiêu đề đề tài")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mô tả")]
        [Display(Name = "Mô tả chi tiết")]
        public string Description { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Yêu cầu đầu vào")]
        public string? Requirements { get; set; }

        [MaxLength(300)]
        [Display(Name = "Công nghệ gợi ý")]
        public string? Technologies { get; set; }

        [Range(1, 10, ErrorMessage = "Số lượng SV từ 1 đến 10")]
        [Display(Name = "Số lượng SV tối đa")]
        public int MaxStudents { get; set; } = 1;

        public int RegistrationPeriodId { get; set; }
    }
}
