using SE_Academic_Affairs_Support_System.ViewModels;

namespace SE_Academic_Affairs_Support_System.Services.AccountManagement
{
    public interface IAccountService
    {
        Task<UserListViewModel> GetUsersAsync(string? keyword, string? roleFilter);
        Task<UserFormViewModel?> GetUserForEditAsync(string userId);
        Task<(bool Success, string Message)> CreateUserAsync(UserFormViewModel vm);
        Task<(bool Success, string Message)> UpdateUserAsync(UserFormViewModel vm);
        Task<(bool Success, string Message)> DeleteUserAsync(string userId);
    }
}
