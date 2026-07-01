# SE Academic Affairs Support System — Hướng dẫn Deploy

Web hỗ trợ học vụ Khoa CNPM (ASP.NET Core 8 MVC + EF Core 9 + SQL Server). Tài liệu này trình bày cách **build, cấu hình và triển khai** dự án.


---

## 1. Yêu cầu môi trường

| Thành phần | Phiên bản |
|---|---|
| .NET SDK | **8.0** (`dotnet --version` ≥ 8.0) |
| EF Core CLI | `dotnet tool install --global dotnet-ef` (hoặc đã có) |
| SQL Server | Azure SQL / SQL Server 2019+ / SQL Express (có quyền tạo bảng) |
| Hosting | IIS / Azure App Service / Linux + Nginx / shared host hỗ trợ .NET 8 |
| (tuỳ chọn) | Visual Studio 2022 nếu publish bằng Publish Profile |

---

## 2. Các file **KHÔNG** có trong Git (phải tự tạo khi deploy)

Repo cố tình `.gitignore` các mục nhạy cảm / runtime sau — **bản clone mới sẽ thiếu**:

| Đường dẫn | Vai trò | Phải làm gì |
|---|---|---|
| `SE Academic Affairs Support System/appsettings.json` | Chuỗi kết nối DB, URL Apps Script, email fallback | **Tự tạo** (mục 3) |
| `SE Academic Affairs Support System/Migrations/` | EF migrations | **Tự scaffold** (mục 4) |
| `SE Academic Affairs Support System/App_Data/` | File báo cáo SV upload | App tự tạo lúc chạy, cần quyền ghi |
| `DataProtectionKeys/` | Khóa mã hóa (mật khẩu email…) | App tự tạo, **phải bền bỉ** (mục 6) |

---

## 3. Cấu hình `appsettings.json`

Tạo file `SE Academic Affairs Support System/appsettings.json` (KHÔNG commit, KHÔNG để lộ secret thật):

```jsonc
{
  "ConnectionStrings": {
    // Tên key BẮT BUỘC là "AzureConnection" (Program.cs dùng key này)
    "AzureConnection": "Data Source=<SERVER>;Initial Catalog=<DB_NAME>;User Id=<USER>;Password=<PASSWORD>;Encrypt=True;TrustServerCertificate=True;"
  },
  "GoogleAppsScript": {
    // URL Web App của Google Apps Script (đồng bộ đề tài/điểm lên Google Sheet)
    "Url": "https://script.google.com/macros/s/XXXXXXXX/exec"
  },
  "EmailFallback": {
    // SMTP mặc định khi CHƯA cấu hình email trong DB (/Admin/EmailConfig)
    "SenderEmail": "youraddress@gmail.com",
    "SenderName": "UIT",
    "AppPassword": "<gmail-app-password-16-ky-tu>",
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": "587",
    "EnableSsl": "true"
  },
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*"
}
```

Ghi chú:
- **DB SQL Server**: dùng connection string MSSQL chuẩn. Nếu là localhost: `Server=.\SQLEXPRESS;Database=SE_Support_System;Trusted_Connection=True;TrustServerCertificate=True;`.
- **Gmail App Password**: bật 2FA Google → tạo App Password (không dùng mật khẩu đăng nhập thường).
- Có thể cấu hình email động sau khi deploy tại `/Admin/EmailConfig`; `EmailFallback` chỉ là dự phòng.

---

## 4. Khởi tạo Database

**Tự động khi khởi động:** `Program.cs` sẽ **tự chạy migration** lúc app start — nếu đã cấu hình `ConnectionStrings:AzureConnection`, nó **tạo DB lần đầu (nếu chưa có) + áp dụng mọi migration đang chờ** (idempotent, không có gì chờ thì bỏ qua). Vì vậy **lần deploy đầu chỉ cần điền connection string rồi chạy app** là DB được tạo.

> Điều kiện: trong assembly **phải có sẵn file migration**. Nếu lỗi tự migrate, app **vẫn khởi động** và ghi log lỗi (sai connection string / thiếu quyền DB / chưa có migration).

```bash
cd "SE Academic Affairs Support System"
dotnet ef migrations add InitialCreate   # tạo migration từ model hiện tại (chỉ cần khi repo chưa có Migrations)
```
Sau đó **chạy app là DB tự tạo**. Hoặc áp dụng thủ công nếu muốn: `dotnet ef database update`.

- Mỗi lần đổi Entity/Model: `dotnet ef migrations add <Tên>` → migration mới sẽ tự áp dụng ở lần khởi động kế tiếp (hoặc `dotnet ef database update`).
- Muốn TẮT auto-migrate: xóa/để trống `AzureConnection` không phải cách đúng — thay vào đó comment khối auto-migrate trong `Program.cs` (ngay sau `builder.Build()`).

---

## 5. Build & chạy thử (local)

```bash
cd "SE Academic Affairs Support System"
dotnet restore
dotnet build -c Release      # phải 0 error
dotnet run                   # https://localhost:7071 và http://localhost:5011
```

Mở trình duyệt vào URL hiển thị trong console.

---

## 6. Data Protection Keys (quan trọng)

App mã hóa mật khẩu SMTP bằng ASP.NET Data Protection, lưu ở thư mục `DataProtectionKeys/` (cạnh app).

- Thư mục này **phải tồn tại lâu dài & cùng một bộ key** giữa các lần restart/scale; mất key ⇒ không giải mã được email config đã lưu (phải nhập lại).
- Trên IIS/host: đảm bảo app có **quyền ghi** vào thư mục content root.
- Khi scale nhiều instance: chia sẻ chung 1 nơi lưu key (file share / blob / Redis) — mặc định hiện tại là file system cục bộ.

---

## 7. Upload báo cáo 500MB

Tính năng nộp báo cáo cho phép file tới **500MB** (.doc/.docx/.pdf/.zip/.rar), lưu local tại `App_Data/reports/{periodId}/`.

- Kestrel + `FormOptions` đã được cấu hình 500MB trong `Program.cs`.
- **Nếu host sau IIS**: thêm vào `web.config` (file publish) trong `<system.webServer>`:
  ```xml
  <security>
    <requestFiltering>
      <requestLimits maxAllowedContentLength="524288000" /> <!-- 500MB -->
    </requestFiltering>
  </security>
  ```
- Đảm bảo thư mục `App_Data` **ghi được** và đủ dung lượng đĩa.

---

## 8. Tạo tài khoản Admin đầu tiên

`SeedAdminController` chỉ hoạt động ở môi trường **Development** (`/SeedAdmin` trả 404 ở Production).

**Cách 1 — chạy Development một lần để seed:**
```bash
set ASPNETCORE_ENVIRONMENT=Development   # Windows (PowerShell: $env:ASPNETCORE_ENVIRONMENT="Development")
dotnet run
```
Mở `/SeedAdmin` → tạo user role **Admin** (và Lecturer/Student mẫu nếu cần) → đăng nhập.

**Cách 2 — seed trực tiếp trên DB Production** (nếu không thể bật Development): tạo user trong `AspNetUsers` + role `Admin` trong `AspNetRoles`/`AspNetUserRoles` (mật khẩu hash theo ASP.NET Identity), và set cột `Role='Admin'`.

> Sau khi có Admin, mọi việc khác (tạo GV/SV, cấu hình email, đợt đăng ký…) làm qua giao diện.

---

## 9. Publish lên hosting

### 9.1. Publish ra thư mục
```bash
cd "SE Academic Affairs Support System"
dotnet publish -c Release -o ./publish
```
Copy toàn bộ `./publish` lên server. Đảm bảo `appsettings.json` (đã điền secret) nằm trong thư mục publish.

### 9.2. Dùng Publish Profile có sẵn (Visual Studio)
Trong `Properties/PublishProfiles/` đã có sẵn profile **FTP / Web Deploy / Zip Deploy** (ví dụ `sesupport - Web Deploy`, `... - FTP`, `... - Zip Deploy`):
- Visual Studio → chuột phải project → **Publish** → chọn profile → **Publish**.
- Hoặc CLI: `dotnet publish -c Release /p:PublishProfile="sesupport - Zip Deploy"`.

### 9.3. IIS / Windows host
1. Cài **.NET 8 Hosting Bundle** trên server.
2. Tạo site/app pool (No Managed Code), trỏ vào thư mục publish.
3. Bổ sung `requestLimits` 500MB (mục 7).
4. Cấp quyền ghi cho `App_Data/` và `DataProtectionKeys/`.

### 9.4. Azure App Service
- Tạo App Service (.NET 8). Đặt connection string & app settings (mục 3) trong **Configuration** thay vì file.
- Bật **Always On** để các background service (nhắc hạn nộp, sync Sheet, đóng đợt) chạy liên tục.

### 9.5. Linux + Nginx (tuỳ chọn)
- `dotnet publish` → chạy bằng `dotnet <app>.dll` dưới systemd; Nginx reverse-proxy; tăng `client_max_body_size 500M;`.

---

## 10. Checklist sau khi deploy

- [ ] `appsettings.json` đã điền `AzureConnection`, `GoogleAppsScript:Url`, `EmailFallback`.
- [ ] `dotnet ef database update` chạy thành công (DB có đủ bảng).
- [ ] Truy cập trang chủ OK (HTTP 200).
- [ ] Tạo được tài khoản **Admin** và đăng nhập.
- [ ] `/Admin/EmailConfig` cấu hình SMTP + **Test gửi** thành công (hoặc dùng EmailFallback).
- [ ] Thư mục `App_Data/` và `DataProtectionKeys/` ghi được.
- [ ] Upload thử 1 file báo cáo lớn (kiểm tra giới hạn 500MB).
- [ ] Các background service hoạt động (host bật Always On / không tự sleep).

---

## 11. Lưu ý vận hành

- **Nguồn sự thật là SQL**; Google Sheet chỉ là bản phản chiếu (đồng bộ qua background service ~mỗi phút).
- Email gửi **fail-safe**: nếu chưa cấu hình SMTP, hệ thống chỉ log và bỏ qua, **không** làm hỏng nghiệp vụ.
- Đổi file `.cshtml` ⇒ phải **build lại + restart** (view compile vào assembly, không hot-reload ở Production).
- **Không commit** `appsettings.json` thật, `App_Data/`, `DataProtectionKeys/`.

