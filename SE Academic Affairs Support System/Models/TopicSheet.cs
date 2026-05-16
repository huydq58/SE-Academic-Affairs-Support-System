namespace SE_Academic_Affairs_Support_System.Models
{
    // Models/Topic.cs
    public class TopicSheet
    {
        public int RowIndex { get; set; }
        public string Stt { get; set; } = "";
        public string TopicName { get; set; } = "";
        public string Lecturer { get; set; } = "";
        public string LecturerInfo { get; set; } = "";
        public string Mssv1 { get; set; } = "";
        public string Student1 { get; set; } = "";
        public string Mssv2 { get; set; } = "";
        public string Student2 { get; set; } = "";
        public string EducationType { get; set; } = "";
        public string Note { get; set; } = "";
        public int Registered { get; set; }
        public int MaxSlot { get; set; }
    }

    // Models/RegisterTopicRequest.cs
    public class RegisterTopicRequest
    {
        public string Action { get; set; } = "register";
        public string SheetId { get; set; } = "";
        public int RowIndex { get; set; }
        public string StudentId { get; set; } = "";
        public string StudentName { get; set; } = "";
    }

    // Models/ApiResponse.cs
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
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
    }
}
