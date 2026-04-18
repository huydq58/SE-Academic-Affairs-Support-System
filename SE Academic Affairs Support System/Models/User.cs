using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
namespace SE_Academic_Affairs_Support_System.Models
{
    public class User : IdentityUser
    {
        public string? FullName { get; set; }
        public string? Role { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

}
