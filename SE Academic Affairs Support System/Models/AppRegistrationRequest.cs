using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // Thêm namespace này

namespace SE_Academic_Affairs_Support_System.Models
{
    // Tạo một Enum để quản lý trạng thái cho chuẩn xác
    public enum RequestStatus
    {
        Pending,
        Processing,
        Approved,
        Rejected
    }

    public class AppRegistrationRequest
    {
        [Key]
        public string RequestId { get; set; }
        public string AppName { get; set; }
        public string AppDescription { get; set; }
        public string Purpose { get; set; }
        public string StudentInfo { get; set; }
        public string StudentEmail { get; set; }
        public string? SupervisorInfo { get; set; }
        public string DemoLink { get; set; }
        public string ApkLink { get; set; }

        // --- CÁC TRƯỜNG MỚI THÊM VÀO ---

        // Trạng thái mặc định khi tạo mới là Pending
        public RequestStatus Status { get; set; } = RequestStatus.Pending;

        // Lưu ID của Lecturer được Admin chọn
        public string? AssignedLecturerId { get; set; }

        // (Tùy chọn) Navigation property nếu bạn dùng Entity Framework Core và IdentityUser
        [ForeignKey("AssignedLecturerId")]
        public virtual User? AssignedLecturer { get; set; }
    }
}