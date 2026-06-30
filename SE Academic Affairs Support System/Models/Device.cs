using System.ComponentModel.DataAnnotations;

namespace SE_Academic_Affairs_Support_System.Models
{
    // Thiết bị quản lý theo TỒN KHO (số lượng), không còn là 1 đơn vị đơn lẻ.
    //   Còn lại (Available) = TotalQuantity - BrokenQuantity - (đang mượn: tổng Quantity của
    //   các DeviceRequestItem thuộc phiếu có Status = "Approved").
    public class Device
    {
        [Key]
        public int DeviceId { get; set; }

        [MaxLength(50)]
        [Display(Name = "Mã thiết bị")]
        public string? DeviceCode { get; set; } // Mã định danh duy nhất, dùng để chống trùng khi import

        [Required(ErrorMessage = "Tên thiết bị không được để trống")]
        [Display(Name = "Tên thiết bị")]
        public string DeviceName { get; set; } // VD: Máy chiếu Panasonic, Micro không dây

        [Display(Name = "Loại thiết bị")]
        public string? Category { get; set; } // VD: Máy chiếu, Âm thanh, Dây cáp...

        [Range(0, int.MaxValue, ErrorMessage = "Tổng số lượng không hợp lệ")]
        [Display(Name = "Tổng số lượng")]
        public int TotalQuantity { get; set; } = 1;

        [Range(0, int.MaxValue, ErrorMessage = "Số lượng hỏng không hợp lệ")]
        [Display(Name = "Số lượng hỏng")]
        public int BrokenQuantity { get; set; } = 0;

        [Display(Name = "Mô tả / Thông số")]
        public string? Description { get; set; }

        [Display(Name = "Hình ảnh minh họa")]
        public string? ImageUrl { get; set; }
    }
}
