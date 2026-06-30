namespace SE_Academic_Affairs_Support_System.Services.EmailNotification
{
    internal static class EmailTemplates
    {
        private static string Layout(string title, string body) => $@"<!DOCTYPE html>
<html><head><meta charset=""UTF-8""><title>{title}</title></head>
<body style=""margin:0;padding:0;background:#f3f4f6;font-family:Arial,sans-serif;"">
<table width=""100%"" cellpadding=""0"" cellspacing=""0"">
<tr><td align=""center"" style=""padding:32px 16px;"">
<table width=""600"" cellpadding=""0"" cellspacing=""0""
  style=""background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);"">
  <tr>
    <td style=""background:#1e3a8a;padding:20px 32px;"">
      <span style=""color:#fff;font-size:18px;font-weight:700;"">Hệ thống Học vụ UIT</span>
    </td>
  </tr>
  <tr><td style=""padding:28px 32px;color:#374151;font-size:14px;line-height:1.7;"">
    {body}
  </td></tr>
  <tr>
    <td style=""background:#f8fafc;padding:14px 32px;border-top:1px solid #e5e7eb;"">
      <p style=""margin:0;font-size:12px;color:#9ca3af;"">
        Email này được gửi tự động từ Hệ thống Hỗ trợ Học vụ - UIT. Vui lòng không trả lời email này.
      </p>
    </td>
  </tr>
</table>
</td></tr></table>
</body></html>";

        private static string TimeStr(TimeSpan t) =>
            $"{t.Hours:D2}:{t.Minutes:D2}";

        public static (string Subject, string Body) RoomBookingConfirmed(
            string userName, string roomName,
            DateTime bookingDate, TimeSpan startTime, TimeSpan endTime, string purpose)
        {
            var subject = $"[Dat phong thanh cong] {roomName} ngay {bookingDate:dd/MM/yyyy}";
            var content = $@"
<p>Xin chao <strong>{userName}</strong>,</p>
<p>Yeu cau dat phong cua ban da duoc xac nhan thanh cong.</p>
<table style=""border-collapse:collapse;width:100%;margin:16px 0;"">
  <tr style=""background:#f8fafc;"">
    <td style=""padding:8px 12px;font-weight:700;width:140px;"">Phong</td>
    <td style=""padding:8px 12px;color:#1e3a8a;font-weight:600;"">{roomName}</td>
  </tr>
  <tr>
    <td style=""padding:8px 12px;font-weight:700;"">Ngay</td>
    <td style=""padding:8px 12px;"">{bookingDate:dd/MM/yyyy}</td>
  </tr>
  <tr style=""background:#f8fafc;"">
    <td style=""padding:8px 12px;font-weight:700;"">Thoi gian</td>
    <td style=""padding:8px 12px;"">{TimeStr(startTime)} - {TimeStr(endTime)}</td>
  </tr>
  <tr>
    <td style=""padding:8px 12px;font-weight:700;"">Muc dich</td>
    <td style=""padding:8px 12px;"">{purpose}</td>
  </tr>
</table>
<p><strong>Luu y:</strong> Vui long nhan chia khoa tai Van phong Khoa, co mat truoc 10 phut, va tat dien/may lanh khi roi phong.</p>
<p>Tran trong,<br/><strong>Bo phan Quan ly Hoc vu - UIT</strong></p>";
            return (subject, Layout(subject, content));
        }

        public static (string Subject, string Body) TopicRegistered(
            string studentName, string topicTitle, string lecturerName, string periodName)
        {
            var subject = $"[Dang ky thanh cong] {topicTitle}";
            var content = $@"
<p>Xin chao <strong>{studentName}</strong>,</p>
<p>Ban da dang ky de tai thanh cong trong dot <strong>{periodName}</strong>.</p>
<div style=""background:#dcfce7;border:1px solid #bbf7d0;border-radius:8px;padding:14px 18px;margin:16px 0;"">
  <strong style=""color:#166534;"">De tai: {topicTitle}</strong><br/>
  <span style=""color:#374151;"">Giang vien huong dan: {lecturerName}</span>
</div>
<p>Chuc ban hoc tap va nghien cuu hieu qua!</p>
<p>Tran trong,<br/><strong>Bo phan Quan ly Hoc vu - UIT</strong></p>";
            return (subject, Layout(subject, content));
        }

        public static (string Subject, string Body) TopicAutoRejected(
            string studentName, string topicTitle, string periodName)
        {
            var subject = "[Thong bao] De xuat de tai bi huy do dot dang ky ket thuc";
            var content = $@"
<p>Xin chao <strong>{studentName}</strong>,</p>
<p>Dot dang ky <strong>{periodName}</strong> da ket thuc.</p>
<div style=""background:#fee2e2;border:1px solid #fecaca;border-radius:8px;padding:14px 18px;margin:16px 0;"">
  <strong style=""color:#991b1b;"">De xuat de tai {topicTitle} cua ban da bi huy tu dong</strong><br/>
  <span style=""color:#6b7280;"">Ly do: dot dang ky da ket thuc ma de xuat chua duoc giang vien duyet.</span>
</div>
<p>Ban co the dang ky lai trong dot tiep theo neu co. Lien he Phong Hoc vu neu can ho tro.</p>
<p>Tran trong,<br/><strong>Bo phan Quan ly Hoc vu - UIT</strong></p>";
            return (subject, Layout(subject, content));
        }

        public static (string Subject, string Body) DeviceBorrowApproved(
            string borrowerName, string deviceName, string purpose)
        {
            var subject = $"[Duyet muon thiet bi] {deviceName}";
            var content = $@"
<p>Xin chao <strong>{borrowerName}</strong>,</p>
<p>Yeu cau muon thiet bi cua ban da duoc <strong style=""color:#166534;"">chap thuan</strong>.</p>
<table style=""border-collapse:collapse;width:100%;margin:16px 0;"">
  <tr style=""background:#f8fafc;"">
    <td style=""padding:8px 12px;font-weight:700;width:140px;"">Thiet bi</td>
    <td style=""padding:8px 12px;color:#1e3a8a;font-weight:600;"">{deviceName}</td>
  </tr>
  <tr>
    <td style=""padding:8px 12px;font-weight:700;"">Muc dich</td>
    <td style=""padding:8px 12px;"">{purpose}</td>
  </tr>
</table>
<p><strong>Luu y:</strong></p>
<ul>
  <li>Vui long mang the sinh vien/nhan vien den Van phong Khoa de ky bien ban ban giao.</li>
  <li>Kiem tra tinh trang thiet bi va phu kien truoc khi nhan.</li>
  <li>Nguoi muon chiu trach nhiem neu co hu hong hoac mat mat.</li>
</ul>
<p>Tran trong,<br/><strong>Bo phan Quan ly Thiet bi - UIT</strong></p>";
            return (subject, Layout(subject, content));
        }

        public static (string Subject, string Body) DeviceBorrowRejected(
            string borrowerName, string deviceName, string? reason)
        {
            var subject = $"[Tu choi muon thiet bi] {deviceName}";
            var reasonHtml = string.IsNullOrWhiteSpace(reason)
                ? ""
                : $@"<p><strong>Ly do:</strong></p>
<div style=""background:#f8fafc;border:1px solid #e5e7eb;border-radius:6px;padding:12px;color:#374151;"">{reason}</div>";
            var content = $@"
<p>Xin chao <strong>{borrowerName}</strong>,</p>
<p>Rat tiec, yeu cau muon thiet bi <strong>{deviceName}</strong> cua ban da bi <strong style=""color:#991b1b;"">tu choi</strong>.</p>
{reasonHtml}
<p>Neu can ho tro, vui long lien he Van phong Khoa.</p>
<p>Tran trong,<br/><strong>Bo phan Quan ly Thiet bi - UIT</strong></p>";
            return (subject, Layout(subject, content));
        }

        public static (string Subject, string Body) DeviceReturned(
            string borrowerName, string deviceName)
        {
            var subject = $"[Xac nhan tra thiet bi] {deviceName}";
            var content = $@"
<p>Xin chao <strong>{borrowerName}</strong>,</p>
<p>He thong da ghi nhan viec ban <strong>tra thiet bi {deviceName}</strong> thanh cong.</p>
<p>Cam on ban da su dung va bao quan thiet bi can than.</p>
<p>Tran trong,<br/><strong>Bo phan Quan ly Thiet bi - UIT</strong></p>";
            return (subject, Layout(subject, content));
        }

        public static (string Subject, string Body) AppSubmitted(
            string studentName, string appName, string requestId)
        {
            var subject = $"[Nhan yeu cau] Dang ky ung dung {appName}";
            var content = $@"
<p>Xin chao <strong>{studentName}</strong>,</p>
<p>He thong da nhan yeu cau dang ky ung dung cua ban va dang cho xet duyet.</p>
<table style=""border-collapse:collapse;width:100%;margin:16px 0;"">
  <tr style=""background:#f8fafc;"">
    <td style=""padding:8px 12px;font-weight:700;width:140px;"">Ung dung</td>
    <td style=""padding:8px 12px;color:#1e3a8a;font-weight:600;"">{appName}</td>
  </tr>
  <tr>
    <td style=""padding:8px 12px;font-weight:700;"">Ma yeu cau</td>
    <td style=""padding:8px 12px;font-family:monospace;"">{requestId}</td>
  </tr>
</table>
<p>Ban se nhan duoc email thong bao ket qua sau khi xet duyet xong.</p>
<p>Tran trong,<br/><strong>Bo phan Quan ly Hoc vu - UIT</strong></p>";
            return (subject, Layout(subject, content));
        }

        public static (string Subject, string Body) AppApproved(
            string studentName, string appName)
        {
            var subject = $"[Duyet thanh cong] Ung dung {appName} da duoc chap thuan";
            var content = $@"
<p>Xin chao <strong>{studentName}</strong>,</p>
<p>Yeu cau dang ky ung dung cua ban da duoc <strong style=""color:#166534;"">chap thuan</strong>.</p>
<div style=""background:#dcfce7;border:1px solid #bbf7d0;border-radius:8px;padding:14px 18px;margin:16px 0;"">
  <strong style=""color:#166534;"">Ung dung: {appName}</strong>
</div>
<p>Tran trong,<br/><strong>Bo phan Quan ly Hoc vu - UIT</strong></p>";
            return (subject, Layout(subject, content));
        }

        public static (string Subject, string Body) AppRejected(
            string studentName, string appName, string? reason)
        {
            var subject = $"[Tu choi] Ung dung {appName} khong duoc chap thuan";
            var reasonHtml = string.IsNullOrWhiteSpace(reason)
                ? ""
                : $@"<p><strong>Ly do:</strong></p>
<div style=""background:#f8fafc;border:1px solid #e5e7eb;border-radius:6px;padding:12px;color:#374151;"">{reason}</div>";
            var content = $@"
<p>Xin chao <strong>{studentName}</strong>,</p>
<p>Rat tiec, yeu cau dang ky ung dung <strong>{appName}</strong> cua ban da bi <strong style=""color:#991b1b;"">tu choi</strong>.</p>
{reasonHtml}
<p>Ban co the chinh sua va gui lai yeu cau, hoac lien he Van phong Khoa de duoc ho tro.</p>
<p>Tran trong,<br/><strong>Bo phan Quan ly Hoc vu - UIT</strong></p>";
            return (subject, Layout(subject, content));
        }

        public static (string Subject, string Body) RoomBookingCancelled(
            string userName, string roomName,
            DateTime bookingDate, TimeSpan startTime, TimeSpan endTime, string? reason)
        {
            var subject = $"[Huy lich dat phong] {roomName} ngay {bookingDate:dd/MM/yyyy}";
            var reasonHtml = string.IsNullOrWhiteSpace(reason)
                ? ""
                : $@"<p><strong>Ly do huy:</strong></p>
<div style=""background:#f8fafc;border:1px solid #e5e7eb;border-radius:6px;padding:12px;color:#374151;"">{reason}</div>";
            var content = $@"
<p>Xin chao <strong>{userName}</strong>,</p>
<p>Rat tiec, lich dat phong cua ban da bi <strong style=""color:#991b1b;"">huy</strong> do Khoa co cong viec dot xuat.</p>
<table style=""border-collapse:collapse;width:100%;margin:16px 0;"">
  <tr style=""background:#f8fafc;"">
    <td style=""padding:8px 12px;font-weight:700;width:140px;"">Phong</td>
    <td style=""padding:8px 12px;color:#1e3a8a;font-weight:600;"">{roomName}</td>
  </tr>
  <tr>
    <td style=""padding:8px 12px;font-weight:700;"">Ngay</td>
    <td style=""padding:8px 12px;"">{bookingDate:dd/MM/yyyy}</td>
  </tr>
  <tr style=""background:#f8fafc;"">
    <td style=""padding:8px 12px;font-weight:700;"">Thoi gian</td>
    <td style=""padding:8px 12px;"">{TimeStr(startTime)} - {TimeStr(endTime)}</td>
  </tr>
</table>
{reasonHtml}
<p>Mong ban thong cam. Vui long dat lai khung gio khac hoac lien he Van phong Khoa de duoc ho tro.</p>
<p>Tran trong,<br/><strong>Bo phan Quan ly Hoc vu - UIT</strong></p>";
            return (subject, Layout(subject, content));
        }

        public static (string Subject, string Body) AppAssignedToLecturer(
            string lecturerName, string appName, string studentInfo, string requestId)
        {
            var subject = $"[Phan cong] Yeu cau dang tai app {appName} can xu ly";
            var content = $@"
<p>Xin chao <strong>{lecturerName}</strong>,</p>
<p>Ban vua duoc phan cong xu ly mot yeu cau dang tai ung dung (CH Play).</p>
<table style=""border-collapse:collapse;width:100%;margin:16px 0;"">
  <tr style=""background:#f8fafc;"">
    <td style=""padding:8px 12px;font-weight:700;width:150px;"">Ung dung</td>
    <td style=""padding:8px 12px;color:#1e3a8a;font-weight:600;"">{appName}</td>
  </tr>
  <tr>
    <td style=""padding:8px 12px;font-weight:700;"">Sinh vien</td>
    <td style=""padding:8px 12px;"">{studentInfo}</td>
  </tr>
  <tr style=""background:#f8fafc;"">
    <td style=""padding:8px 12px;font-weight:700;"">Ma yeu cau</td>
    <td style=""padding:8px 12px;font-family:monospace;"">{requestId}</td>
  </tr>
</table>
<p>Vui long dang nhap he thong, vao muc <strong>Yeu cau dang tai App</strong> de xem chi tiet va duyet/tu choi.</p>
<p>Tran trong,<br/><strong>Bo phan Quan ly Hoc vu - UIT</strong></p>";
            return (subject, Layout(subject, content));
        }

        public static (string Subject, string Body) ReportDeadline(
            string studentName, string periodName, DateTime deadline, bool isReminder, int? daysLeft)
        {
            var subject = isReminder
                ? $"[Nhac nho] Con {daysLeft} ngay den han nop bao cao - {periodName}"
                : $"[Thong bao] Han nop bao cao do an - {periodName}";

            var headline = isReminder
                ? $@"<div style=""background:#fef3c7;border:1px solid #fde68a;border-radius:8px;padding:14px 18px;margin:16px 0;color:#92400e;"">
  <strong>Chi con {daysLeft} ngay</strong> den han nop bao cao. Vui long hoan tat va nop dung han.
</div>"
                : $@"<div style=""background:#dbeafe;border:1px solid #bfdbfe;border-radius:8px;padding:14px 18px;margin:16px 0;color:#1e40af;"">
  He thong vua cap nhat <strong>han nop bao cao</strong> cho dot dang ky cua ban.
</div>";

            var content = $@"
<p>Xin chao <strong>{studentName}</strong>,</p>
<p>Dot: <strong>{periodName}</strong></p>
{headline}
<table style=""border-collapse:collapse;width:100%;margin:16px 0;"">
  <tr style=""background:#f8fafc;"">
    <td style=""padding:8px 12px;font-weight:700;width:160px;"">Han nop bao cao</td>
    <td style=""padding:8px 12px;color:#991b1b;font-weight:700;"">{deadline:dd/MM/yyyy HH:mm}</td>
  </tr>
</table>
<p>Vui long dang nhap he thong va nop file bao cao (.doc/.docx/.pdf/.zip/.rar, toi da 500MB) truoc thoi han tren.</p>
<p>Tran trong,<br/><strong>Bo phan Quan ly Hoc vu - UIT</strong></p>";
            return (subject, Layout(subject, content));
        }

        public static (string Subject, string Body) DeviceDamaged(
            string borrowerName, string damageSummary)
        {
            var subject = "[Thong bao] Thiet bi ban giao bi hu hong";
            var content = $@"
<p>Xin chao <strong>{borrowerName}</strong>,</p>
<p>Khi nhan tra, he thong ghi nhan mot so thiet bi ban da muon bi <strong style=""color:#991b1b;"">hu hong</strong>:</p>
<div style=""background:#fee2e2;border:1px solid #fecaca;border-radius:8px;padding:14px 18px;margin:16px 0;color:#7f1d1d;"">
  {damageSummary}
</div>
<p>Vui long lien he Van phong Khoa de phoi hop xu ly (sua chua / den bu) theo quy dinh.</p>
<p>Tran trong,<br/><strong>Bo phan Quan ly Thiet bi - UIT</strong></p>";
            return (subject, Layout(subject, content));
        }
    }
}
