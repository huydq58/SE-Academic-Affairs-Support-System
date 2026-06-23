namespace SE_Academic_Affairs_Support_System.Models
{
    // Models/TopicSheet.cs — ánh xạ DanhSachDeTai sheet
    public class TopicSheet
    {
        public int RowIndex { get; set; }
        public int Stt { get; set; }           // STT (số nguyên)
        public int TopicId { get; set; }       // DB Topic.Id
        public string TopicName { get; set; } = "";
        public string Description { get; set; } = "";
        public string Requirements { get; set; } = "";
        public string Technologies { get; set; } = "";
        public int MaxSlot { get; set; }
        public string Lecturer { get; set; } = "";
        public string LecturerInfo { get; set; } = ""; // LecturerCode
        public string Mssv1 { get; set; } = "";   // MSSV sinh viên đã đăng ký
        public string Student1 { get; set; } = ""; // Tên sinh viên đã đăng ký
        public string Note { get; set; } = "";     // Ghi chú (duyệt, hướng dẫn...)
        public int Registered { get; set; }        // Số SV đã đăng ký (tính từ Mssv1)
    }

    // Models/RegisterTopicRequest.cs
    public class RegisterTopicRequest
    {
        public string SheetId { get; set; } = string.Empty;
        public int RowIndex { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;

        // Sinh viên 2 (tuỳ chọn)
        public string? StudentName2 { get; set; }
        public string? StudentId2 { get; set; }
    }

    // Models/ApiResponse.cs
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int? RowIndex { get; set; }
    }

    // Models/AddTopicRequest.cs
    public class AddTopicRequest
    {
        public string SheetId { get; set; } = string.Empty;
        public int TopicId { get; set; }
        public string TopicTitle { get; set; } = string.Empty;
        public string TopicDescription { get; set; } = string.Empty;
        public string? Technologies { get; set; }
        public string? Requirements { get; set; }
        public int MaxStudents { get; set; }
        public string LecturerName { get; set; } = string.Empty;
        public string LecturerCode { get; set; } = string.Empty;
        public string? Note { get; set; }
    }

    // ViewModels/TopicListViewModel.cs
    public class TopicListSheetViewModel
    {
        public string PeriodName { get; set; } = "";
        public string CourseName { get; set; } = "";
        public string SheetId { get; set; } = "";
        public List<TopicSheet> Topics { get; set; } = [];
        public string? StatusMessage { get; set; }
        public bool IsSuccess { get; set; }
        public int PeriodId { get; set; }

        public List<TopicRegistration> Registrations { get; set; } = [];
        public TopicRegistration? GetRegistration(int rowIndex) =>
        Registrations.FirstOrDefault(r => r.RowIndex == rowIndex);
    }
}
