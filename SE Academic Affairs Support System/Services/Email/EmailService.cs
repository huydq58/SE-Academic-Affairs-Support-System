using System.Net;
using System.Net.Mail;
using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.Services.Email
{

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendConfirmRoomAsync(string toEmail, string fullName, TimeSpan startTime, TimeSpan endTime, DateTime bookingDate, string purPose)
        {
            var smtp = new SmtpClient("smtp.gmail.com", 587)
            {

                Credentials = new NetworkCredential(
    "huydq58422@gmail.com",
                    "gslg vnml bnph oesx"
                ),
                EnableSsl = true
            };



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
                From = new MailAddress("huydq58422@gmail.com", "UIT"),
                Subject = "Thông báo chấp nhận yêu cầu",
                Body = body,
                IsBodyHtml = true
            };

            mail.To.Add(toEmail);

            await smtp.SendMailAsync(mail);
        }

        public async Task SendConfirmDeviceAsync(string toEmail, string fullName, DeviceRequest deviceRequest)
        {
            var smtp = new SmtpClient("smtp.gmail.com", 587)
            {

                Credentials = new NetworkCredential(
    "huydq58422@gmail.com",
                    "gslg vnml bnph oesx"
                ),
                EnableSsl = true
            };



            var body = $@"
<p>Xin chào <strong>{fullName}</strong>,</p>
<p>Phòng Quản lý Học vụ xác nhận yêu cầu mượn thiết bị của bạn đã được phê duyệt.</p>

<p><strong>Thông tin chi tiết đơn mượn:</strong><br />
- Thiết bị: {deviceRequest.Device.DeviceName}<br />
- Địa điểm sử dụng: Phòng E7.3 - Tòa nhà E</p>

<p><strong>Lưu ý dành cho người mượn:</strong><br />
1. Nhận thiết bị: Vui lòng mang theo thẻ nhân viên/sinh viên đến Văn phòng Khoa để đối chiếu và ký biên bản bàn giao.<br />
2. 2. Kiểm tra: Vui lòng kiểm tra tình trạng máy, phụ kiện (dây sạc, túi đựng, chuột...) trước khi rời khỏi văn phòng.<br />
3. 3. Bảo quản: Người mượn chịu trách nhiệm hoàn toàn nếu xảy ra hư hỏng hoặc mất mát thiết bị trong thời gian sử dụng.<br />
4. Trả thiết bị: Vui lòng hoàn trả thiết bị đúng thời gian quy định để phục vụ cho các tiết học tiếp theo.</p>

<p>Trân trọng,<br />
<strong>Bộ phận Quản lý Thiết bị Học vụ.</strong></p>"; var mail = new MailMessage
            {
                From = new MailAddress("huydq58422@gmail.com", "UIT"),
                Subject = "Thông báo chấp nhận yêu cầu",
                Body = body,
                IsBodyHtml = true
            };

            mail.To.Add(toEmail);

            await smtp.SendMailAsync(mail);
        }
        public async Task SendConfirmAppAsync(string toEmail, string fullName, AppRegistrationRequest appRequest)
        {
            var smtp = new SmtpClient("smtp.gmail.com", 587)
            {

                Credentials = new NetworkCredential(
    "huydq58422@gmail.com",
                    "gslg vnml bnph oesx"
                ),
                EnableSsl = true
            };



            var body = $@"
<p>Xin chào <strong>{fullName}</strong>,</p>
<p>Phòng Quản lý Học vụ xác nhận yêu cầu đăng tải ứng dụng của bạn đã được phê duyệt.</p>

<p><strong>Thông tin chi tiết:</strong><br />
- Ứng dụng: {appRequest.AppName}<br />
- Tác giả: {appRequest.StudentInfo}</p>

<p>Trân trọng,<br />
<strong>Bộ phận Quản lý Học vụ.</strong></p>"; var mail = new MailMessage
            {
                From = new MailAddress("huydq58422@gmail.com", "UIT"),
                Subject = "Thông báo chấp nhận yêu cầu đăng tải App",
                Body = body,
                IsBodyHtml = true
            };

            mail.To.Add(toEmail);

            await smtp.SendMailAsync(mail);
        }

    }




}
