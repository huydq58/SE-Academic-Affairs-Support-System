using SE_Academic_Affairs_Support_System.ViewModels;

namespace SE_Academic_Affairs_Support_System.Services.ProjectRegistration
{
    public interface IRegistrationPeriodStudentService
    {
        /// <summary>
        /// Trả về danh sách tất cả sinh viên với trạng thái đã chọn cho đợt đăng ký.
        /// </summary>
        Task<List<StudentCheckboxItem>> GetAvailableStudentsAsync(int periodId);

        /// <summary>
        /// Parse file TXT/CSV (mỗi dòng là một MSSV) hoặc XLSX (cột đầu là MSSV) rồi trả về danh sách StudentProfileId.
        /// </summary>
        Task<(List<int> StudentIds, string? Error)> ParseStudentFileAsync(IFormFile file);

        /// <summary>
        /// Ghi đè toàn bộ danh sách sinh viên được phép cho đợt đăng ký.
        /// </summary>
        Task SaveAllowedStudentsAsync(int periodId, List<int> studentProfileIds);
    }
}
