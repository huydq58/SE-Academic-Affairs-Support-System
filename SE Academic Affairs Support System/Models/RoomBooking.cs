using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SE_Academic_Affairs_Support_System.Models
{
    public class RoomBooking
    {
        [Key]
        public int BookingId { get; set; }
        public int RoomId { get; set; }
        public string UserName { get; set; }

        public string UserEmail { get; set; }
        public string PhoneNumber { get; set; }

        [DataType(DataType.Date)]
        public DateTime BookingDate { get; set; }

        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public string Purpose { get; set; }
        public string Status { get; set; } = "Pending"; // Pending / Approved / Cancelled
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Thông tin hủy (admin hủy lịch khi khoa có việc) — lưu để xử lý về sau
        [MaxLength(500)]
        public string? CancelReason { get; set; }
        public DateTime? CancelledAt { get; set; }
        [MaxLength(200)]
        public string? CancelledBy { get; set; }

        public RoomModel Room { get; set; }

    }
}
