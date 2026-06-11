// ViewModels/GradingViewModel.cs
using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.ViewModels
{
    // ViewModels/GradingListViewModel.cs — thêm PeriodId và SyncStatus vào Row
    public class GradingListViewModel
    {
        public string PeriodName { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string SheetId { get; set; } = string.Empty;
        public int PeriodId { get; set; }   // thêm mới
        public string SearchQuery { get; set; } = string.Empty;
        public List<GradingSheetRow> Rows { get; set; } = [];
    }

    // ViewModels/GradeFormViewModel.cs — thêm PeriodId
    public class GradeFormViewModel
    {
        public string SheetId { get; set; } = string.Empty;
        public int PeriodId { get; set; }   // thêm mới
        public GradingSheetRow Row { get; set; } = null!;
        public decimal Score { get; set; } = 5;
    }
}
