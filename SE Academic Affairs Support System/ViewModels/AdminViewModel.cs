using System.ComponentModel.DataAnnotations;

namespace SE_Academic_Affairs_Support_System.ViewModels
{
    // ── Admin: Period Management ──────────────────────────────────────────────
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

        public bool IsActive { get; set; }
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
