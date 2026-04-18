using SE_Academic_Affairs_Support_System.Models;
namespace SE_Academic_Affairs_Support_System.Services.NotificationSevices
{
    public interface INotificationService
    {
        Task SendAsync(string userId, string message, string? actionUrl = null);
        Task<List<Notification>> GetUnreadAsync(string userId);
        Task MarkReadAsync(int notificationId, string userId);
    }

}
