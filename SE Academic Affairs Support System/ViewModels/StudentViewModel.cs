using System.ComponentModel.DataAnnotations;
using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.ViewModels
{
    // ── Student: Browse & Register ────────────────────────────────────────────
    public class TopicListViewModel
    {
        public RegistrationPeriod Period { get; set; } = null!;
        public List<TopicCardViewModel> Topics { get; set; } = new();
        public string? SearchKeyword { get; set; }
        public int? FilterLecturerId { get; set; }
        public List<LecturerSelectItem> Lecturers { get; set; } = new();
    }

    public class TopicCardViewModel
    {
        public int TopicId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Requirements { get; set; }
        public string? Technologies { get; set; }
        public string LecturerName { get; set; } = string.Empty;
        public int LecturerProfileId { get; set; }
        public int MaxStudents { get; set; }
        public int RegisteredCount { get; set; }
        public int AvailableSlots => MaxStudents - RegisteredCount;
        public bool CanRegister => AvailableSlots > 0;
        public bool AlreadyRegistered { get; set; }
    }

    public class LecturerSelectItem
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string LecturerCode { get; set; } = string.Empty;
    }

    // ── Student: Propose New Topic (Flow B) ───────────────────────────────────
    public class ProposalViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đề tài")]
        [MaxLength(300, ErrorMessage = "Tên đề tài tối đa 300 ký tự")]
        [Display(Name = "Tên đề tài")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mô tả mục tiêu")]
        [Display(Name = "Mục tiêu / Mô tả ngắn")]
        public string Description { get; set; } = string.Empty;

        [MaxLength(300)]
        [Display(Name = "Công nghệ dự kiến sử dụng")]
        public string? Technologies { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn giảng viên hướng dẫn")]
        [Display(Name = "Giảng viên hướng dẫn")]
        public int LecturerProfileId { get; set; }

        public int RegistrationPeriodId { get; set; }

        // Populated for revision re-submit
        public int? ExistingRegistrationId { get; set; }
        public string? PreviousLecturerNote { get; set; }

        public List<LecturerSelectItem> AvailableLecturers { get; set; } = new();
    }

    // ── Student: My Registrations Dashboard ──────────────────────────────────
    public class MyRegistrationsViewModel
    {
        public List<RegistrationRowViewModel> Registrations { get; set; } = new();
        public bool HasActivePeriod { get; set; }
        public int? ActivePeriodId { get; set; }
    }

    public class RegistrationRowViewModel
    {
        public int RegistrationId { get; set; }
        public string TopicTitle { get; set; } = string.Empty;
        public string LecturerName { get; set; } = string.Empty;
        public string PeriodName { get; set; } = string.Empty;
        public RegistrationStatus Status { get; set; }
        public string StatusLabel => Status switch
        {
            RegistrationStatus.PENDING => "Chờ duyệt",
            RegistrationStatus.REVISION_REQUIRED => "Cần chỉnh sửa",
            RegistrationStatus.APPROVED => "Đã duyệt",
            RegistrationStatus.REJECTED => "Bị từ chối",
            _ => "Không xác định"
        };
        public string StatusCssClass => Status switch
        {
            RegistrationStatus.PENDING => "badge-warning",
            RegistrationStatus.REVISION_REQUIRED => "badge-info",
            RegistrationStatus.APPROVED => "badge-success",
            RegistrationStatus.REJECTED => "badge-danger",
            _ => "badge-secondary"
        };
        public string? LecturerNote { get; set; }
        public bool CanRevise => Status == RegistrationStatus.REVISION_REQUIRED;
        public bool CanCancel => Status == RegistrationStatus.PENDING || Status == RegistrationStatus.REVISION_REQUIRED;
        public DateTime CreatedAt { get; set; }
    }

}
