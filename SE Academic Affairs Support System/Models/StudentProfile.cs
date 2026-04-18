using System.ComponentModel.DataAnnotations;

namespace SE_Academic_Affairs_Support_System.Models
{
    public class StudentProfile
    {
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string StudentCode { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;

        public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
    }

}
