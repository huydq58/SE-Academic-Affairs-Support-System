using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.Services.AppRegistration
{
    public interface IAppRegistrationService
    {
        Task CreateRequestAsync(AppRegistrationRequest request);
        Task<AppRegistrationRequest?> GetByIdAsync(string requestId);
        Task<IEnumerable<AppRegistrationRequest>> GetAllAsync();
    }
}
