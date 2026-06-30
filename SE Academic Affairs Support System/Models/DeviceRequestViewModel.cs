using System.ComponentModel.DataAnnotations;

namespace SE_Academic_Affairs_Support_System.Models
{
    // Một dòng thiết bị có thể chọn trên form mượn (multi-device).
    public class BorrowItemInput
    {
        public int DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty; // hiển thị
        public string? Category { get; set; }                  // hiển thị
        public int Available { get; set; }                     // số lượng còn có thể mượn
        public bool Selected { get; set; }
        public int Quantity { get; set; } = 1;
    }

    // Dòng hiển thị tồn kho thiết bị (Index/Catalog).
    public class DeviceInventoryRow
    {
        public Device Device { get; set; } = null!;
        public int Borrowed { get; set; }   // đang được mượn (tổng Quantity các phiếu Approved)
        public int Available => Device.TotalQuantity - Device.BrokenQuantity - Borrowed;
    }

    // Form nhận trả: mỗi thiết bị có thể ghi nhận số hư hỏng + người làm hỏng + lý do.
    public class ReturnItemInput
    {
        public int DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public int BorrowedQuantity { get; set; }     // số đã mượn (giới hạn số hỏng)
        public int DamagedQuantity { get; set; }      // số bị hỏng khi trả
        public string? DamagedByName { get; set; }    // người làm hư hỏng
        public string? Reason { get; set; }           // lý do hư hỏng
    }

    public class ReturnFormViewModel
    {
        public int RequestId { get; set; }
        public string BorrowerName { get; set; } = string.Empty;
        public List<ReturnItemInput> Items { get; set; } = new();
    }

    // Form đăng ký mượn: thông tin người mượn + danh sách thiết bị chọn.
    public class CreateBorrowViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [Display(Name = "Họ và tên")]
        public string BorrowerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string BorrowerEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mục đích mượn")]
        [Display(Name = "Mục đích sử dụng")]
        public string Purpose { get; set; } = string.Empty;

        public List<BorrowItemInput> Items { get; set; } = new();
    }
}
