using System.ComponentModel.DataAnnotations;

namespace SE_Academic_Affairs_Support_System.ViewModels
{
    public enum ExportTimeMode
    {
        Today,      // Hôm nay (00:00 → cuối ngày)
        ThisWeek,   // Tuần này: từ Thứ Hai đầu tuần (00:00) → cuối ngày hôm nay
        Range,      // Khoảng thời gian tùy chọn [FromDate, ToDate], inclusive 2 đầu
        All         // Không lọc thời gian
    }

    /// <summary>
    /// Tham số lọc dùng chung cho các endpoint export Excel.
    /// Mốc thời gian tính theo giờ địa phương (khớp với cách lưu RequestDate/CreatedAt = DateTime.Now).
    /// </summary>
    public class ExportFilterViewModel
    {
        public ExportTimeMode TimeMode { get; set; } = ExportTimeMode.All;

        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        // App: tên enum RequestStatus (Pending/Processing/Approved/Rejected).
        // Device: "Returned" / "NotReturned". null hoặc rỗng = tất cả.
        public string? Status { get; set; }

        public (DateTime? Start, DateTime? End, string? Error) ResolveRange()
            => ResolveRange(TimeMode, FromDate, ToDate);

        /// <summary>
        /// Quy ra cận [Start, End] inclusive theo chế độ thời gian (giờ địa phương).
        /// Error != null nếu Range thiếu ngày hoặc From &gt; To.
        /// </summary>
        public static (DateTime? Start, DateTime? End, string? Error) ResolveRange(
            ExportTimeMode mode, DateTime? from, DateTime? to)
        {
            var today = DateTime.Today;
            var endOfToday = today.AddDays(1).AddTicks(-1);

            switch (mode)
            {
                case ExportTimeMode.Today:
                    return (today, endOfToday, null);

                case ExportTimeMode.ThisWeek:
                    // Thứ Hai là đầu tuần
                    int diff = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                    return (today.AddDays(-diff), endOfToday, null);

                case ExportTimeMode.Range:
                    if (from == null || to == null)
                        return (null, null, "Vui lòng chọn cả 'Từ ngày' và 'Đến ngày' cho khoảng thời gian.");
                    if (from.Value.Date > to.Value.Date)
                        return (null, null, "'Từ ngày' không được sau 'Đến ngày'.");
                    // Chuẩn hóa: From về 00:00, To về cuối ngày để không sót bản ghi trong ngày cuối
                    return (from.Value.Date, to.Value.Date.AddDays(1).AddTicks(-1), null);

                case ExportTimeMode.All:
                default:
                    return (null, null, null);
            }
        }

        /// <summary>Hậu tố tên file phản ánh chế độ thời gian.</summary>
        public string TimeModeSlug() => TimeMode switch
        {
            ExportTimeMode.Today => "HomNay",
            ExportTimeMode.ThisWeek => "TuanNay",
            ExportTimeMode.Range => "Khoang",
            _ => "TatCa"
        };
    }
}
