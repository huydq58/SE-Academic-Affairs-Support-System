using System.Net;
using System.Net.Mail;

namespace SE_Academic_Affairs_Support_System.Services.Email
{

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendTicketAsync(string toEmail, string fullName)
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
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Xác nhận yêu cầu của bạn đã được duyệt</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #2c3e50; margin: 0; padding: 0; background-color: #f8f9fa; }}
        .wrapper {{ width: 100%; table-layout: fixed; background-color: #f8f9fa; padding-bottom: 40px; }}
        .main-container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; border-top: 5px solid #1a73e8; box-shadow: 0 2px 5px rgba(0,0,0,0.1); }}
        .header {{ padding: 25px; text-align: center; background-color: #ffffff; }}
        .content {{ padding: 30px; border-top: 1px solid #f0f0f0; }}
        .title {{ color: #1a73e8; font-size: 20px; font-weight: bold; margin-bottom: 15px; }}
        .info-box {{ background-color: #f1f8ff; border-radius: 6px; padding: 20px; margin: 20px 0; }}
        .info-table {{ width: 100%; border-collapse: collapse; }}
        .info-table td {{ padding: 8px 0; font-size: 15px; vertical-align: top; }}
        .label {{ font-weight: bold; color: #5f6368; width: 120px; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #70757a; background-color: #f8f9fa; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #1a73e8; color: #ffffff !important; text-decoration: none; border-radius: 4px; font-weight: bold; margin-top: 15px; }}
        .note {{ font-size: 13px; color: #d93025; font-style: italic; margin-top: 15px; }}
    </style>
</head>
<body>
    <div class=""wrapper"">
        <div class=""main-container"">
            <div class=""header"">
                <h2 style=""margin:0;"">PHÒNG QUẢN LÝ HỌC VỤ</h2>
                <p style=""margin:5px 0 0; color: #5f6368; font-size: 14px;"">Thông báo xác nhận mượn phòng</p>
            </div>

            <div class=""content"">
                <div class=""title"">Xác nhận đặt phòng thành công</div>
                <p>Kính gửi Thầy/Cô <strong>{fullName}</strong>,</p>
                <p>Hệ thống quản lý học vụ xác nhận yêu cầu mượn phòng của Thầy/Cô đã được phê duyệt. Thông tin chi tiết như sau:</p>

                <div class=""info-box"">
                    <table class=""info-table"">

                        <tr>
                            <td class=""label"">Phòng: E7.3</td>
                            <td><strong>Phòng E7.3</strong></td>
                        </tr>
                        <tr>
                            <td class=""label"">Thời gian:</td>

                        </tr>
                        <tr>
                            <td class=""label"">Mục đích:</td>
                            <td>[Tiết dạy/Hội thảo/Họp chuyên môn]</td>
                        </tr>
                        <tr>
                            <td class=""label"">Thiết bị kèm:</td>
                            <td>Máy chiếu, Micro, Loa, [Thiết_Bị_Khác]</td>
                        </tr>
                    </table>
                </div>

                <p>Thầy/Cô vui lòng liên hệ bộ phận Kỹ thuật/Bảo vệ tại tầng G để nhận chìa khóa hoặc hỗ trợ mở cửa phòng.</p>
                
                <div style=""text-align: center;"">
                    <a href=""#"" class=""button"">Xem lịch biểu chi tiết</a>
                </div>

                <p class=""note"">
                    * Lưu ý: Nếu có thay đổi hoặc hủy lịch, vui lòng thực hiện trên hệ thống trước ít nhất 12 giờ để điều phối cho đơn vị khác.
                </p>
            </div>

            <div class=""footer"">
                <p>Đây là email tự động từ Hệ thống Quản lý Học vụ - [Tên Trường/Trung Tâm]</p>
                <p>Hotline hỗ trợ kỹ thuật: (028) xxxx xxxx | Email: hocvu@edu.vn</p>
            </div>
        </div>
    </div>
</body>
</html>
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
    }




}
