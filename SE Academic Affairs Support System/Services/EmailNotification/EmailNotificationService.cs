using SE_Academic_Affairs_Support_System.Services.Email;

namespace SE_Academic_Affairs_Support_System.Services.EmailNotification
{
    public class EmailNotificationService : IEmailNotificationService
    {
        private readonly IEmailService _email;
        private readonly ILogger<EmailNotificationService> _logger;

        public EmailNotificationService(IEmailService email, ILogger<EmailNotificationService> logger)
        {
            _email = email;
            _logger = logger;
        }

        public async Task NotifyRoomBookingConfirmedAsync(
            string toEmail, string userName, string roomName,
            DateTime bookingDate, TimeSpan startTime, TimeSpan endTime, string purpose)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("NotifyRoomBookingConfirmed: email người dùng null, bỏ qua.");
                return;
            }
            try
            {
                var (subject, body) = EmailTemplates.RoomBookingConfirmed(
                    userName, roomName, bookingDate, startTime, endTime, purpose);
                await _email.SendAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email xác nhận đặt phòng tới {Email}", toEmail);
            }
        }

        public async Task NotifyTopicRegisteredAsync(
            string toEmail, string studentName, string topicTitle,
            string lecturerName, string periodName)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("NotifyTopicRegistered: email sinh viên null, bỏ qua.");
                return;
            }
            try
            {
                var (subject, body) = EmailTemplates.TopicRegistered(
                    studentName, topicTitle, lecturerName, periodName);
                await _email.SendAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email xác nhận đăng ký đề tài tới {Email}", toEmail);
            }
        }

        public async Task NotifyTopicAutoRejectedAsync(
            string toEmail, string studentName, string topicTitle, string periodName)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("NotifyTopicAutoRejected: email sinh viên null, bỏ qua.");
                return;
            }
            try
            {
                var (subject, body) = EmailTemplates.TopicAutoRejected(studentName, topicTitle, periodName);
                await _email.SendAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email auto-reject đề tài tới {Email}", toEmail);
            }
        }

        public async Task NotifyDeviceBorrowApprovedAsync(
            string toEmail, string borrowerName, string deviceName, string purpose)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("NotifyDeviceBorrowApproved: email người mượn null, bỏ qua.");
                return;
            }
            try
            {
                var (subject, body) = EmailTemplates.DeviceBorrowApproved(borrowerName, deviceName, purpose);
                await _email.SendAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email duyệt mượn thiết bị tới {Email}", toEmail);
            }
        }

        public async Task NotifyDeviceBorrowRejectedAsync(
            string toEmail, string borrowerName, string deviceName, string? reason)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("NotifyDeviceBorrowRejected: email người mượn null, bỏ qua.");
                return;
            }
            try
            {
                var (subject, body) = EmailTemplates.DeviceBorrowRejected(borrowerName, deviceName, reason);
                await _email.SendAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email từ chối mượn thiết bị tới {Email}", toEmail);
            }
        }

        public async Task NotifyDeviceReturnedAsync(
            string toEmail, string borrowerName, string deviceName)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("NotifyDeviceReturned: email người mượn null, bỏ qua.");
                return;
            }
            try
            {
                var (subject, body) = EmailTemplates.DeviceReturned(borrowerName, deviceName);
                await _email.SendAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email xác nhận trả thiết bị tới {Email}", toEmail);
            }
        }

        public async Task NotifyAppSubmittedAsync(
            string toEmail, string studentName, string appName, string requestId)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("NotifyAppSubmitted: email sinh viên null, bỏ qua.");
                return;
            }
            try
            {
                var (subject, body) = EmailTemplates.AppSubmitted(studentName, appName, requestId);
                await _email.SendAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email xác nhận nộp app tới {Email}", toEmail);
            }
        }

        public async Task NotifyAppApprovedAsync(
            string toEmail, string studentName, string appName)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("NotifyAppApproved: email sinh viên null, bỏ qua.");
                return;
            }
            try
            {
                var (subject, body) = EmailTemplates.AppApproved(studentName, appName);
                await _email.SendAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email duyệt app tới {Email}", toEmail);
            }
        }

        public async Task NotifyAppRejectedAsync(
            string toEmail, string studentName, string appName, string? reason)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("NotifyAppRejected: email sinh viên null, bỏ qua.");
                return;
            }
            try
            {
                var (subject, body) = EmailTemplates.AppRejected(studentName, appName, reason);
                await _email.SendAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email từ chối app tới {Email}", toEmail);
            }
        }

        public async Task NotifyRoomBookingCancelledAsync(
            string toEmail, string userName, string roomName,
            DateTime bookingDate, TimeSpan startTime, TimeSpan endTime, string? reason)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("NotifyRoomBookingCancelled: email người dùng null, bỏ qua.");
                return;
            }
            try
            {
                var (subject, body) = EmailTemplates.RoomBookingCancelled(
                    userName, roomName, bookingDate, startTime, endTime, reason);
                await _email.SendAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email hủy đặt phòng tới {Email}", toEmail);
            }
        }

        public async Task NotifyDeviceDamagedAsync(
            string toEmail, string borrowerName, string damageSummary)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("NotifyDeviceDamaged: email người mượn null, bỏ qua.");
                return;
            }
            try
            {
                var (subject, body) = EmailTemplates.DeviceDamaged(borrowerName, damageSummary);
                await _email.SendAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email báo hư hỏng thiết bị tới {Email}", toEmail);
            }
        }

        public async Task NotifyReportDeadlineAsync(
            string toEmail, string studentName, string periodName,
            DateTime deadline, bool isReminder, int? daysLeft)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("NotifyReportDeadline: email sinh viên null, bỏ qua.");
                return;
            }
            try
            {
                var (subject, body) = EmailTemplates.ReportDeadline(studentName, periodName, deadline, isReminder, daysLeft);
                await _email.SendAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email hạn nộp báo cáo tới {Email}", toEmail);
            }
        }

        public async Task NotifyAppAssignedToLecturerAsync(
            string toEmail, string lecturerName, string appName, string studentInfo, string requestId)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("NotifyAppAssignedToLecturer: email giảng viên null, bỏ qua.");
                return;
            }
            try
            {
                var (subject, body) = EmailTemplates.AppAssignedToLecturer(lecturerName, appName, studentInfo, requestId);
                await _email.SendAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email giao app cho giảng viên {Email}", toEmail);
            }
        }
    }
}
