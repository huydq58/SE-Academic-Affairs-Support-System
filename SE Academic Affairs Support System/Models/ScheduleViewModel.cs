namespace SE_Academic_Affairs_Support_System.Models
{
    public class ScheduleViewModel
    {
        public DateTime WeekStartDate { get; set; } 
        public DateTime WeekEndDate { get; set; } 
        public string UserName { get; set; }

        public List<DateTime> DaysInWeek { get; set; }

        public List<RoomModel> Rooms { get; set; }
        public List<TimeSlot> TimeSlots { get; set; }

        public List<RoomBooking> WeeklyBookings { get; set; }
    }
}
