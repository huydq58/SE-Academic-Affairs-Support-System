using System.ComponentModel.DataAnnotations;

namespace SE_Academic_Affairs_Support_System.ViewModels
{
    public class UserRowViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Mssv { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UserListViewModel
    {
        public List<UserRowViewModel> Users { get; set; } = new();
        public string? SearchKeyword { get; set; }
        public string? RoleFilter { get; set; }
    }

    public class UserFormViewModel
    {
        public string? Id { get; set; } // null = create mode

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [Display(Name = "Họ tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn vai trò")]
        [Display(Name = "Vai trò")]
        public string Role { get; set; } = "Student";

        [Display(Name = "MSSV / Mã GV")]
        public string? Mssv { get; set; }

        [Display(Name = "Mật khẩu")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Mật khẩu ít nhất 6 ký tự")]
        public string? Password { get; set; }

        [Display(Name = "Xác nhận mật khẩu")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Mật khẩu không khớp")]
        public string? ConfirmPassword { get; set; }

        public bool IsEditMode => !string.IsNullOrEmpty(Id);
    }
}
