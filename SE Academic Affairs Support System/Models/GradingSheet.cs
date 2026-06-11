// Models/GradingSheet.cs
namespace SE_Academic_Affairs_Support_System.Models
{
    /// <summary>
    /// Một dòng dữ liệu chấm điểm lưu trên Google Sheet "BangDiem"
    /// </summary>
    public class GradingSheet
    {
        public int    RowIndex   { get; set; }   // vị trí dòng trên sheet (để update)
        public string Mssv       { get; set; } = "";
        public string StudentName{ get; set; } = "";
        public string TopicName  { get; set; } = "";
        public string Lecturer   { get; set; } = "";
        public float? Score      { get; set; }   // null = chưa chấm
        public string GradedBy   { get; set; } = "";
        public string GradedAt   { get; set; } = "";
    }

    /// <summary>Request gửi lên Apps Script để lưu/cập nhật điểm</summary>
    public class GradeTopicRequest
    {
        public string SheetId    { get; set; } = "";
        public string Mssv       { get; set; } = "";
        public float  Score      { get; set; }
        public string GradedBy   { get; set; } = "";
    }



    // Models/GradingSheet.cs (hoặc nơi định nghĩa GradingSheetRow) — thêm SyncStatus
    public class GradingSheetRow
    {
        public int RowIndex { get; set; }
        public string Mssv { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string TopicName { get; set; } = string.Empty;
        public string Lecturer { get; set; } = string.Empty;
        public double? Score { get; set; }
        public string GradedBy { get; set; } = string.Empty;
        public string GradedAt { get; set; } = string.Empty;
        public SyncStatus? SyncStatus { get; set; } 
    }
}
