using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SE_Academic_Affairs_Support_System.Models
{
    public class RoomBooking
    {
        [Key]
        public int BookingId { get; set; }

        [Required]
        public int RoomId { get; set; }

        [Required]
        public int SlotId { get; set; }

        [Required]
        public DateTime BookingDate { get; set; }

        // Người gửi yêu cầu
        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(255)]
        public string Purpose { get; set; }

        // Pending / Approved / Rejected / Cancelled
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        // Người duyệt
        public int? ApproverId { get; set; }

        // Lý do từ chối
        [StringLength(255)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;


        // Navigation Properties (khuyến nghị)
        [ForeignKey("RoomId")]
        public RoomModel? Room { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [ForeignKey("ApproverId")]
        public User? Approver { get; set; }

        [ForeignKey("SlotId")]
        public TimeSlot? Slot { get; set; }
        
    }

}
