namespace SE_Academic_Affairs_Support_System.Services.EmailNotification
{
    public interface IEmailNotificationService
    {
        Task NotifyRoomBookingConfirmedAsync(
            string toEmail, string userName, string roomName,
            DateTime bookingDate, TimeSpan startTime, TimeSpan endTime, string purpose);

        Task NotifyTopicRegisteredAsync(
            string toEmail, string studentName, string topicTitle,
            string lecturerName, string periodName);

        Task NotifyTopicAutoRejectedAsync(
            string toEmail, string studentName, string topicTitle, string periodName);

        Task NotifyDeviceBorrowApprovedAsync(
            string toEmail, string borrowerName, string deviceName, string purpose);

        Task NotifyDeviceBorrowRejectedAsync(
            string toEmail, string borrowerName, string deviceName, string? reason);

        Task NotifyDeviceReturnedAsync(
            string toEmail, string borrowerName, string deviceName);

        Task NotifyAppSubmittedAsync(
            string toEmail, string studentName, string appName, string requestId);

        Task NotifyAppApprovedAsync(
            string toEmail, string studentName, string appName);
    }
}
