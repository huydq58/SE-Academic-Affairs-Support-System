using System.ComponentModel.DataAnnotations;

namespace SE_Academic_Affairs_Support_System.Models
{
    public class Registration
    {
        public int Id { get; set; }

        public int StudentProfileId { get; set; }
        public StudentProfile Student { get; set; } = null!;

        public int TopicId { get; set; }
        public Topic Topic { get; set; } = null!;

        public int LecturerProfileId { get; set; }
        public LecturerProfile Lecturer { get; set; } = null!;

        public int RegistrationPeriodId { get; set; }
        public RegistrationPeriod RegistrationPeriod { get; set; } = null!;

        public RegistrationStatus Status { get; set; } = RegistrationStatus.PENDING;

        // Revision / reject notes from lecturer
        [MaxLength(1000)]
        public string? LecturerNote { get; set; }

        // Student's proposed content (for flow B)
        [MaxLength(300)]
        public string? ProposedTitle { get; set; }
        public string? ProposedDescription { get; set; }
        [MaxLength(300)]
        public string? ProposedTechnologies { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Iteration count for revision tracking
        public int RevisionCount { get; set; } = 0;
    }
}
