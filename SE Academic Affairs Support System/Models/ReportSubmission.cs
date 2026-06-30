using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SE_Academic_Affairs_Support_System.Models
{
    // Bài nộp báo cáo đồ án của sinh viên cho một đợt (1 SV ↔ 1 bài/đợt, nộp lại sẽ thay thế).
    public class ReportSubmission
    {
        [Key]
        public int Id { get; set; }

        public int StudentProfileId { get; set; }
        [ForeignKey(nameof(StudentProfileId))]
        public StudentProfile? Student { get; set; }

        public int RegistrationPeriodId { get; set; }
        [ForeignKey(nameof(RegistrationPeriodId))]
        public RegistrationPeriod? Period { get; set; }

        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;        // tên file gốc

        [MaxLength(255)]
        public string StoredFileName { get; set; } = string.Empty;  // tên lưu trên đĩa (App_Data)

        [MaxLength(100)]
        public string? ContentType { get; set; }

        public long FileSize { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.Now;
    }
}
