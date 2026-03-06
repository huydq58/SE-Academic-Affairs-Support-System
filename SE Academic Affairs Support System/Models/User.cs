using System.ComponentModel.DataAnnotations;
namespace SE_Academic_Affairs_Support_System.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; } // BCrypt hash


        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

}
