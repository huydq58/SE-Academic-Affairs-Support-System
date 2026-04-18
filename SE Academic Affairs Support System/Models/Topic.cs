using System.ComponentModel.DataAnnotations;

namespace SE_Academic_Affairs_Support_System.Models
{
    public enum TopicStatus
    {
        Open,       // GV tạo, đang mở đăng ký
        Closed,     // Hết slot hoặc đã khóa
        Proposed,   // SV đề xuất – chờ GV duyệt
    }

    public class Topic
    {
        public int Id { get; set; }

        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Requirements { get; set; }

        [MaxLength(300)]
        public string? Technologies { get; set; }

        public int MaxStudents { get; set; } = 1;
        public TopicStatus Status { get; set; } = TopicStatus.Open;

        // Who owns this topic
        public int LecturerProfileId { get; set; }
        public LecturerProfile Lecturer { get; set; } = null!;

        // If proposed by a student
        public int? ProposedByStudentId { get; set; }
        public StudentProfile? ProposedByStudent { get; set; }

        public int RegistrationPeriodId { get; set; }
        public RegistrationPeriod RegistrationPeriod { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
    }
}
