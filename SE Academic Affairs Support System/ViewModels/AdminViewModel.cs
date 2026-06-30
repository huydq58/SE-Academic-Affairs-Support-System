using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SE_Academic_Affairs_Support_System.ViewModels
{
    // ── Admin: Period Management ──────────────────────────────────────────────
    public class StudentCheckboxItem
    {
        public int StudentProfileId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    public class PeriodFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên đợt")]
        [MaxLength(200)]
        [Display(Name = "Tên đợt đăng ký")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên môn học")]
        [MaxLength(100)]
        [Display(Name = "Môn học")]
        public string CourseName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Ngày bắt đầu")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Ngày kết thúc")]
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(14);

        [Display(Name = "Hạn nộp báo cáo")]
        public DateTime? ReportDeadline { get; set; }

        [Required]
        [Url(ErrorMessage = "Link Google Sheet không hợp lệ")]
        public string? GoogleSheetLink { get; set; }
        public bool IsActive { get; set; }

        // Giới hạn sinh viên đăng ký
        public bool RestrictToAllowedStudents { get; set; } = false;
        public List<int> SelectedStudentIds { get; set; } = new();
        public IFormFile? StudentListFile { get; set; }
        // Danh sách để render checkbox (không submit lại)
        public List<StudentCheckboxItem> AvailableStudents { get; set; } = new();
    }

    // ── Admin: Import Topics ──────────────────────────────────────────────────
    public class ImportTopicsViewModel
    {
        public int PeriodId { get; set; }
        public string PeriodName { get; set; } = string.Empty;
        public IFormFile? File { get; set; }
        // Results (populated after POST)
        public bool IsProcessed { get; set; }
        public int Created { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = [];
    }

    // ── Admin: Export ─────────────────────────────────────────────────────────
    public class ExportRowViewModel
    {
        public string StudentCode { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;
        public string LecturerName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
