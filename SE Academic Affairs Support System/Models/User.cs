using Microsoft.AspNetCore.Identity;

namespace SE_Academic_Affairs_Support_System.Models
{
    public class User : IdentityUser
    {
        public string? FullName { get; set; }
        public string? Role { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        // Nullable cho Admin/Lecturer; unique được enforce bằng filtered index WHERE Mssv IS NOT NULL
        public string? Mssv { get; set; }
    }
}
