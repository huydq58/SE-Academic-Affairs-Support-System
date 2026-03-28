using System.ComponentModel.DataAnnotations;

namespace SE_Academic_Affairs_Support_System.Models
{
    public class Device
    {
        [Key]
        public int DeviceId { get; set; }

        [Required(ErrorMessage = "Tên thiết bị không được để trống")]
        [Display(Name = "Tên thiết bị")]
        public string DeviceName { get; set; } // VD: Máy chiếu Panasonic 01, Micro không dây 02

        [Display(Name = "Loại thiết bị")]
        public string Category { get; set; } // VD: Máy chiếu, Âm thanh, Dây cáp...

        [Display(Name = "Tình trạng hiện tại")]
        public string Condition { get; set; } = "Good"; // Good (Tốt), Broken (Hỏng)

        [Display(Name = "Trạng thái mượn")]

        public string Status { get; set; } = "Available";

        [Display(Name = "Mô tả / Thông số")]
        public string Description { get; set; }

        [Display(Name = "Hình ảnh minh họa")]
        public string ImageUrl { get; set; } 
    }
}
