using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SE_Academic_Affairs_Support_System.Models
{
    // Chi tiết mượn — mỗi dòng là MỘT thiết bị trong phiếu mượn, kèm số lượng.
    public class DeviceRequestItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RequestId { get; set; }
        [ForeignKey(nameof(RequestId))]
        public DeviceRequest Request { get; set; } = null!;

        [Required]
        public int DeviceId { get; set; }
        [ForeignKey(nameof(DeviceId))]
        public Device? Device { get; set; }

        [Display(Name = "Số lượng")]
        public int Quantity { get; set; } = 1;
    }
}
