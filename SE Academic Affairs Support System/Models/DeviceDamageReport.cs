using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SE_Academic_Affairs_Support_System.Models
{
    // Ghi nhận hư hỏng phát hiện khi nhận trả: lưu thiết bị, người mượn, người làm hỏng, lý do.
    public class DeviceDamageReport
    {
        [Key]
        public int Id { get; set; }

        public int RequestId { get; set; }
        [ForeignKey(nameof(RequestId))]
        public DeviceRequest? Request { get; set; }

        public int DeviceId { get; set; }
        [ForeignKey(nameof(DeviceId))]
        public Device? Device { get; set; }

        [MaxLength(200)]
        public string DeviceName { get; set; } = string.Empty;   // snapshot (giữ được kể cả khi đổi tên/xóa)

        [MaxLength(200)]
        public string BorrowerName { get; set; } = string.Empty;  // người mượn (snapshot)

        [MaxLength(200)]
        public string DamagedByName { get; set; } = string.Empty; // người làm hư hỏng

        [Display(Name = "Số lượng hỏng")]
        public int Quantity { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Lý do hư hỏng")]
        public string? Reason { get; set; }

        [Display(Name = "Ngày ghi nhận")]
        public DateTime ReportedAt { get; set; } = DateTime.Now;
    }
}
