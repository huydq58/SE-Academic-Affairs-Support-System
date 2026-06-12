namespace SE_Academic_Affairs_Support_System.Models
{
    // Bảng trung gian: giới hạn sinh viên được phép đăng ký trong một đợt
    public class RegistrationPeriodStudent
    {
        public int Id { get; set; }

        public int RegistrationPeriodId { get; set; }
        public RegistrationPeriod RegistrationPeriod { get; set; } = null!;

        public int StudentProfileId { get; set; }
        public StudentProfile StudentProfile { get; set; } = null!;

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
