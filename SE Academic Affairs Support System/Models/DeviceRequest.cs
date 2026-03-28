using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;

namespace SE_Academic_Affairs_Support_System.Models
{
    public class DeviceRequest
    {
        [Key]
        public int RequestId { get; set; }

        [Required]
        public int DeviceId { get; set; }

        [ForeignKey("DeviceId")]

        public Device? Device { get; set; }

        [Required]
        [Display(Name = "Họ tên người mượn")]
        public string BorrowerName { get; set; }

        [Display(Name = "Email người mượn")]
        public string BorrowerEmail { get; set; }

        [Display(Name = "Mục đích mượn")]
        public string Purpose { get; set; }


        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = "Pending";

        [Display(Name = "Lý do từ chối")]
        public string? RejectReason { get; set; }

        [Display(Name = "Ngày yêu cầu")]
        public DateTime RequestDate { get; set; } = DateTime.Now;

        [Display(Name = "Ngày trả")]
        public DateTime? ReturnDate { get; set; }
    }
}
