using System.ComponentModel.DataAnnotations;

namespace SE_Academic_Affairs_Support_System.ViewModels
{
    public class EmailConfigListItemViewModel
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; }
        public bool EnableSsl { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class EmailConfigFormViewModel
    {
        public int Id { get; set; }
        public bool IsEditMode => Id > 0;

        [Required(ErrorMessage = "Vui lòng nhập tên hiển thị")]
        [MaxLength(100)]
        [Display(Name = "Tên hiển thị")]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email gửi")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        [MaxLength(200)]
        [Display(Name = "Email gửi")]
        public string SenderEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên người gửi")]
        [MaxLength(100)]
        [Display(Name = "Tên người gửi")]
        public string SenderName { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "App Password")]
        public string? AppPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập SMTP Host")]
        [MaxLength(200)]
        [Display(Name = "SMTP Host")]
        public string SmtpHost { get; set; } = "smtp.gmail.com";

        [Required(ErrorMessage = "Vui lòng nhập SMTP Port")]
        [Range(1, 65535, ErrorMessage = "Port phải từ 1 đến 65535")]
        [Display(Name = "SMTP Port")]
        public int SmtpPort { get; set; } = 587;

        [Display(Name = "Bật SSL")]
        public bool EnableSsl { get; set; } = true;
    }

    public class EmailConfigTestViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập địa chỉ email nhận")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        [Display(Name = "Email nhận thử")]
        public string TestEmail { get; set; } = string.Empty;

        public int ConfigId { get; set; }
    }
}
