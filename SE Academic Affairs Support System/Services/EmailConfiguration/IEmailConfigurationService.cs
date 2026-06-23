using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.ViewModels;

namespace SE_Academic_Affairs_Support_System.Services.EmailConfig
{
    public interface IEmailConfigurationService
    {
        Task<List<EmailConfigListItemViewModel>> GetAllAsync();
        Task<EmailConfigFormViewModel?> GetForEditAsync(int id);
        Task<EmailConfiguration?> GetActiveAsync();
        Task<EmailConfiguration?> GetActiveEntityByIdAsync(int id);
        Task<(bool Success, string Message)> CreateAsync(EmailConfigFormViewModel vm);
        Task<(bool Success, string Message)> UpdateAsync(EmailConfigFormViewModel vm);
        Task<(bool Success, string Message)> DeleteAsync(int id);
        Task<(bool Success, string Message)> SetActiveAsync(int id);
    }
}
