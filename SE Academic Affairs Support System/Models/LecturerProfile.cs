using System.ComponentModel.DataAnnotations;

namespace SE_Academic_Affairs_Support_System.Models
{
    public class LecturerProfile
    {
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string LecturerCode { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;

        // Max students this lecturer accepts in a registration period
        public int MaxStudents { get; set; } = 10;

        public ICollection<Topic> Topics { get; set; } = new List<Topic>();
        public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
    }

}
