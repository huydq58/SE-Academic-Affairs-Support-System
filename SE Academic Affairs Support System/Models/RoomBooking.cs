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
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public RoomModel Room { get; set; }

    }
}
