using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.Services.Email
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _db;
        private readonly IDataProtector _protector;

        public EmailService(IConfiguration config, AppDbContext db, IDataProtectionProvider dpProvider)
        {
            _config = config;
            _db = db;
            _protector = dpProvider.CreateProtector("EmailConfig.AppPassword");
        }

        public async Task SendConfirmRoomAsync(string toEmail, string fullName, TimeSpan startTime, TimeSpan endTime, DateTime bookingDate, string purPose)
        {
            var (smtp, senderEmail, senderName) = await CreateSmtpAsync();
            var body = $@"
<p>Xin chào {fullName},</p>

<p>Phòng Quản lý Học vụ xác nhận yêu cầu mượn phòng của bạn đã được phê duyệt. Thông tin chi tiết:</p>

<p>
<b>Phòng:</b> E7.3 - Tòa nhà E<br>
<b>Thời gian:</b> {startTime} đến {endTime}<br>
<b>Ngày:</b> {bookingDate:dd/MM/yyyy}<br>
<b>Mục đích:</b> {purPose}
</p>

<p><b>Lưu ý:</b></p>
<ul>
    <li>Vui lòng nhận chìa khóa và bàn giao thiết bị tại Văn phòng Khoa.</li>
    <li>Có mặt trước 10 phút để kiểm tra thiết bị.</li>
    <li>Vui lòng tắt điện và máy lạnh trước khi rời khỏi phòng.</li>
</ul>
";
            var mail = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = "Thông báo chấp nhận yêu cầu",
                Body = body,
                IsBodyHtml = true
            };
            mail.To.Add(toEmail);
            await smtp.SendMailAsync(mail);
        }

        public async Task SendConfirmDeviceAsync(string toEmail, string fullName, DeviceRequest deviceRequest)
        {
            var (smtp, senderEmail, senderName) = await CreateSmtpAsync();
            var body = $@"
<p>Xin chào <strong>{fullName}</strong>,</p>
<p>Phòng Quản lý Học vụ xác nhận yêu cầu mượn thiết bị của bạn đã được phê duyệt.</p>

<p><strong>Thông tin chi tiết đơn mượn:</strong><br />
- Thiết bị: {deviceRequest.Device.DeviceName}<br />
- Địa điểm sử dụng: Phòng E7.3 - Tòa nhà E</p>

<p><strong>Lưu ý dành cho người mượn:</strong><br />
1. Nhận thiết bị: Vui lòng mang theo thẻ nhân viên/sinh viên đến Văn phòng Khoa để đối chiếu và ký biên bản bàn giao.<br />
2. Kiểm tra: Vui lòng kiểm tra tình trạng máy, phụ kiện (dây sạc, túi đựng, chuột...) trước khi rời khỏi văn phòng.<br />
3. Bảo quản: Người mượn chịu trách nhiệm hoàn toàn nếu xảy ra hư hỏng hoặc mất mát thiết bị trong thời gian sử dụng.<br />
4. Trả thiết bị: Vui lòng hoàn trả thiết bị đúng thời gian quy định để phục vụ cho các tiết học tiếp theo.</p>

<p>Trân trọng,<br />
<strong>Bộ phận Quản lý Thiết bị Học vụ.</strong></p>";
            var mail = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = "Thông báo chấp nhận yêu cầu",
                Body = body,
                IsBodyHtml = true
            };
            mail.To.Add(toEmail);
            await smtp.SendMailAsync(mail);
        }

        public async Task SendConfirmAppAsync(string toEmail, string fullName, AppRegistrationRequest appRequest)
        {
            var (smtp, senderEmail, senderName) = await CreateSmtpAsync();
            var body = $@"
<p>Xin chào <strong>{fullName}</strong>,</p>
<p>Phòng Quản lý Học vụ xác nhận yêu cầu đăng tải ứng dụng của bạn đã được phê duyệt.</p>

<p><strong>Thông tin chi tiết:</strong><br />
- Ứng dụng: {appRequest.AppName}<br />
- Tác giả: {appRequest.StudentInfo}</p>

<p>Trân trọng,<br />
<strong>Bộ phận Quản lý Học vụ.</strong></p>";
            var mail = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = "Thông báo chấp nhận yêu cầu đăng tải App",
                Body = body,
                IsBodyHtml = true
            };
            mail.To.Add(toEmail);
            await smtp.SendMailAsync(mail);
        }

        public async Task<bool> SendTopicProposalToLecturerAsync(
            string toEmail, string lecturerName,
            string studentName, string studentCode,
            string topicTitle, string description,
            string reviewUrl)
        {
            var (smtp, senderEmail, _) = await CreateSmtpAsync();
            var body = $@"
<p>Xin chào <strong>{lecturerName}</strong>,</p>
<p>Sinh viên <strong>{studentName}</strong> ({studentCode}) vừa gửi đề xuất đề tài mới chờ bạn xem xét.</p>

<table style=""border-collapse:collapse; width:100%; font-size:14px;"">
  <tr><td style=""padding:6px 12px; font-weight:700; color:#374151; width:140px;"">Tên đề tài</td>
      <td style=""padding:6px 12px; color:#1e3a8a; font-weight:600;"">{topicTitle}</td></tr>
  <tr style=""background:#f8fafc;""><td style=""padding:6px 12px; font-weight:700; color:#374151;"">Sinh viên</td>
      <td style=""padding:6px 12px;"">{studentName} ({studentCode})</td></tr>
  <tr><td style=""padding:6px 12px; font-weight:700; color:#374151; vertical-align:top;"">Mô tả</td>
      <td style=""padding:6px 12px; color:#6b7280; line-height:1.6;"">{description}</td></tr>
</table>

<p style=""margin-top:20px;"">
  <a href=""{reviewUrl}"" style=""display:inline-block; padding:10px 20px; background:#2563eb; color:#fff; border-radius:6px; text-decoration:none; font-weight:700;"">
    Xem &amp; Duyệt đề xuất
  </a>
</p>

<p style=""font-size:12px; color:#9ca3af;"">Bạn nhận được email này vì sinh viên đã chọn bạn là giảng viên hướng dẫn.</p>
<p>Trân trọng,<br/><strong>Hệ thống Học vụ UIT</strong></p>";

            var mail = new MailMessage
            {
                From = new MailAddress(senderEmail, "Học vụ UIT"),
                Subject = $"[Đề xuất đề tài] {studentName} – {topicTitle}",
                Body = body,
                IsBodyHtml = true
            };
            mail.To.Add(toEmail);

            try
            {
                await smtp.SendMailAsync(mail);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> SendTopicDecisionToStudentAsync(
            string toEmail, string studentName,
            string topicTitle, TopicDecisionType decision,
            string? reason, string? actionUrl)
        {
            var (smtp, senderEmail, _) = await CreateSmtpAsync();

            string subject, statusHtml, reasonHtml, actionHtml;

            switch (decision)
            {
                case TopicDecisionType.Approve:
                    subject = $"[Đề tài ĐƯỢC DUYỆT] {topicTitle}";
                    statusHtml = @"<div style=""background:#dcfce7; border:1px solid #bbf7d0; border-radius:8px; padding:12px 16px; margin:16px 0;"">
                      <strong style=""color:#166534;"">✓ Đề xuất của bạn đã được DUYỆT!</strong>
                    </div>";
                    reasonHtml = string.IsNullOrWhiteSpace(reason)
                        ? ""
                        : $@"<p><strong>Ghi chú từ giảng viên:</strong> {reason}</p>";
                    actionHtml = actionUrl != null
                        ? $@"<p><a href=""{actionUrl}"" style=""display:inline-block; padding:10px 20px; background:#166534; color:#fff; border-radius:6px; text-decoration:none; font-weight:700;"">Xem đăng ký của tôi</a></p>"
                        : "";
                    break;

                case TopicDecisionType.Revise:
                    subject = $"[Cần chỉnh sửa] {topicTitle}";
                    statusHtml = @"<div style=""background:#fef3c7; border:1px solid #fde68a; border-radius:8px; padding:12px 16px; margin:16px 0;"">
                      <strong style=""color:#92400e;"">⚠ Giảng viên yêu cầu bạn chỉnh sửa đề xuất.</strong>
                    </div>";
                    reasonHtml = string.IsNullOrWhiteSpace(reason)
                        ? ""
                        : $@"<p><strong>Nội dung cần chỉnh sửa:</strong></p>
                             <div style=""background:#f8fafc; border:1px solid #e5e7eb; border-radius:6px; padding:12px; color:#374151; line-height:1.6;"">{reason}</div>";
                    actionHtml = actionUrl != null
                        ? $@"<p style=""margin-top:16px;""><a href=""{actionUrl}"" style=""display:inline-block; padding:10px 20px; background:#d97706; color:#fff; border-radius:6px; text-decoration:none; font-weight:700;"">Chỉnh sửa đề xuất</a></p>"
                        : "";
                    break;

                default: // Reject
                    subject = $"[Đề tài bị từ chối] {topicTitle}";
                    statusHtml = @"<div style=""background:#fee2e2; border:1px solid #fecaca; border-radius:8px; padding:12px 16px; margin:16px 0;"">
                      <strong style=""color:#991b1b;"">✗ Đề xuất của bạn đã bị từ chối.</strong>
                    </div>";
                    reasonHtml = string.IsNullOrWhiteSpace(reason)
                        ? ""
                        : $@"<p><strong>Lý do:</strong> {reason}</p>";
                    actionHtml = actionUrl != null
                        ? $@"<p><a href=""{actionUrl}"" style=""display:inline-block; padding:10px 20px; background:#2563eb; color:#fff; border-radius:6px; text-decoration:none; font-weight:700;"">Xem danh sách đề tài</a></p>"
                        : "";
                    break;
            }

            var body = $@"
<p>Xin chào <strong>{studentName}</strong>,</p>
<p>Đây là thông báo về đề xuất đề tài <strong>{topicTitle}</strong> của bạn:</p>
{statusHtml}
{reasonHtml}
{actionHtml}
<p>Trân trọng,<br/><strong>Hệ thống Học vụ UIT</strong></p>";

            var mail = new MailMessage
            {
                From = new MailAddress(senderEmail, "Học vụ UIT"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            mail.To.Add(toEmail);

            try
            {
                await smtp.SendMailAsync(mail);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            var (smtp, senderEmail, senderName) = await CreateSmtpAsync();
            var mail = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            mail.To.Add(toEmail);
            await smtp.SendMailAsync(mail);
        }

        private async Task<(SmtpClient Smtp, string SenderEmail, string SenderName)> CreateSmtpAsync()
        {
            var active = await _db.EmailConfigurations.FirstOrDefaultAsync(e => e.IsActive);
            if (active != null)
            {
                var password = _protector.Unprotect(active.EncryptedAppPassword);
                return (
                    new SmtpClient(active.SmtpHost, active.SmtpPort)
                    {
                        Credentials = new NetworkCredential(active.SenderEmail, password),
                        EnableSsl = active.EnableSsl
                    },
                    active.SenderEmail,
                    active.SenderName
                );
            }

            // Fallback to appsettings
            var fallbackEmail = _config["EmailFallback:SenderEmail"] ?? "huydq58422@gmail.com";
            var fallbackName = _config["EmailFallback:SenderName"] ?? "UIT";
            var fallbackPassword = _config["EmailFallback:AppPassword"] ?? string.Empty;
            var fallbackHost = _config["EmailFallback:SmtpHost"] ?? "smtp.gmail.com";
            var fallbackPort = int.TryParse(_config["EmailFallback:SmtpPort"], out var p) ? p : 587;
            var fallbackSsl = !string.Equals(_config["EmailFallback:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase);

            return (
                new SmtpClient(fallbackHost, fallbackPort)
                {
                    Credentials = new NetworkCredential(fallbackEmail, fallbackPassword),
                    EnableSsl = fallbackSsl
                },
                fallbackEmail,
                fallbackName
            );
        }
    }
}
