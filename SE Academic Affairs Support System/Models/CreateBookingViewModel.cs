using System;
using System.ComponentModel.DataAnnotations;

namespace SE_Academic_Affairs_Support_System.ViewModels
{
    public class CreateBookingViewModel
    {
        // Các trường này chỉ để hiển thị (Read-only)
        public int RoomId { get; set; }
        public string RoomName { get; set; }

        public int SlotId { get; set; }
        public string SlotName { get; set; }
        public string SlotTime { get; set; } // Hiển thị giờ cụ thể (VD: 07:00 - 09:15)

        public DateTime BookingDate { get; set; }

        // Các trường người dùng phải nhập
        [Required(ErrorMessage = "Vui lòng nhập mục đích sử dụng phòng.")]
        [MaxLength(200, ErrorMessage = "Mục đích không được vượt quá 200 ký tự.")]
        public string Purpose { get; set; }

        [Required(ErrorMessage = "Vui lòng để lại số điện thoại liên hệ.")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [RegularExpression(@"^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Định dạng số điện thoại chưa đúng.")]
        public string PhoneNumber { get; set; }
    }
}