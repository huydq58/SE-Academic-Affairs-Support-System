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

        // Mail từ chối yêu cầu đăng tải app (CH Play)
        Task NotifyAppRejectedAsync(
            string toEmail, string studentName, string appName, string? reason);

        // Mail thông báo hủy lịch đặt phòng (admin hủy)
        Task NotifyRoomBookingCancelledAsync(
            string toEmail, string userName, string roomName,
            DateTime bookingDate, TimeSpan startTime, TimeSpan endTime, string? reason);

        // Mail báo sinh viên về thiết bị bàn giao bị hư hỏng khi trả
        Task NotifyDeviceDamagedAsync(
            string toEmail, string borrowerName, string damageSummary);

        // Mail thông báo / nhắc hạn nộp báo cáo đồ án
        Task NotifyReportDeadlineAsync(
            string toEmail, string studentName, string periodName,
            DateTime deadline, bool isReminder, int? daysLeft);

        // Mail báo giảng viên có yêu cầu đăng tải app cần xử lý (admin vừa gán)
        Task NotifyAppAssignedToLecturerAsync(
            string toEmail, string lecturerName, string appName, string studentInfo, string requestId);
    }
}
