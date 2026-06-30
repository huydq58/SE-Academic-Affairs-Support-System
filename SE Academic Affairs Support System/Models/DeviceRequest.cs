using System.ComponentModel.DataAnnotations;

namespace SE_Academic_Affairs_Support_System.Models
{
    // Phiếu mượn (header) — một phiếu gồm NHIỀU thiết bị (xem DeviceRequestItem).
    // Trạng thái trả nằm ở MỨC PHIẾU: ReturnDate/Status áp cho cả phiếu (trả đồng loạt).
    public class DeviceRequest
    {
        [Key]
        public int RequestId { get; set; }

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

        // Chi tiết mượn: mỗi dòng 1 thiết bị + số lượng
        public ICollection<DeviceRequestItem> Items { get; set; } = new List<DeviceRequestItem>();
    }
}
