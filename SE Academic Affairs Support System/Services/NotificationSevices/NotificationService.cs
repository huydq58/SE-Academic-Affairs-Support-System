using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.Services.NotificationSevices
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _db;

        public NotificationService(AppDbContext db)
        {
            _db = db;
        }

        public async Task SendAsync(string userId, string message, string? actionUrl = null)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Message = message,
                ActionUrl = actionUrl,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        public async Task<List<Notification>> GetUnreadAsync(string userId)
            => await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

        public async Task MarkReadAsync(int notificationId, string userId)
        {
            var n = await _db.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
            if (n != null) { n.IsRead = true; await _db.SaveChangesAsync(); }
        }
    }

}
