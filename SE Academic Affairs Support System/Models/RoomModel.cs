using System.ComponentModel.DataAnnotations;

namespace SE_Academic_Affairs_Support_System.Models
{
    public class RoomModel
    {
        [Key]
        public int RoomID { get; set; }
        public string RoomName { get; set; }

        public string Condition { get; set; }
    }
}
