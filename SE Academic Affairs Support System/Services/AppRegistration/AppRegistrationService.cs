using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.Services.AppRegistration
{
    public class AppRegistrationService : IAppRegistrationService
    {
        private readonly AppDbContext _context;


        public AppRegistrationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateRequestAsync(AppRegistrationRequest request)
        {
            _context.AppRegistrationRequests.Add(request);
            await _context.SaveChangesAsync();
        }

        public async Task<AppRegistrationRequest?> GetByIdAsync(string requestId)
        {
            return await _context.AppRegistrationRequests.FindAsync(requestId);
        }

        public async Task<IEnumerable<AppRegistrationRequest>> GetAllAsync()
        {
            return await Task.FromResult(_context.AppRegistrationRequests.ToList());
        }
    }
}
