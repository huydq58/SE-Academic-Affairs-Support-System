using System.ComponentModel.DataAnnotations;

namespace SE_Academic_Affairs_Support_System.Models
{
    public enum RegistrationStatus
    {
        PENDING = 0,            // Chờ GV duyệt (đề tài đề xuất)
        APPROVED = 1,           // Đã duyệt / Đăng ký thành công
        REJECTED = 2,            // Từ chối / Hết slot
        REVISION_REQUIRED=3,  // GV yêu cầu sửa đổi
    }

    public class RegistrationPeriod
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty; // e.g. "Đồ án tốt nghiệp – HK1 2025"

        [Required, MaxLength(100)]
        public string CourseName { get; set; } = string.Empty;
        [Url(ErrorMessage = "Link Google Sheet không hợp lệ")]
        public string? GoogleSheetLink { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; } = false;

        public ICollection<Topic> Topics { get; set; } = new List<Topic>();
        public ICollection<Registration> Registrations { get; set; } = new List<Registration>();

        public string GetDownloadLink(string format)
        {
            if (string.IsNullOrEmpty(GoogleSheetLink)) return "#";

            // Tìm vị trí bắt đầu của ID file (sau /d/) và vị trí kết thúc (trước /edit hoặc /view)
            try
            {
                var parts = GoogleSheetLink.Split('/');
                var fileId = parts[5]; // Thông thường ID nằm ở vị trí này trong URL chuẩn
                return $"https://docs.google.com/spreadsheets/d/{fileId}/export?format={format}";
            }
            catch
            {
                return GoogleSheetLink; // Trả về link gốc nếu parse lỗi
            }
        }

    }

}
