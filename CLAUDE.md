# SE Academic Affairs Support System — CLAUDE.md

## 1. Tóm tắt

Ứng dụng web hỗ trợ công tác học vụ cho một trường đại học Việt Nam (SE-UIT). Ba vai trò: Admin, Giảng viên (Lecturer), Sinh viên (Student). Tính năng cốt lõi: đăng ký / đề xuất đề tài luận văn (quy trình phê duyệt đa bước), xem danh sách đề tài từ Google Sheet, đặt phòng, mượn thiết bị, đăng ký phần mềm, chấm điểm đồng bộ lên Google Sheets.

---

## 2. Tech Stack & Chạy

| Thứ | Chi tiết |
|---|---|
| Framework | ASP.NET Core 8 MVC, EF Core 9 |
| DB chính | SQL Server (Azure), key `AzureConnection` trong `appsettings.json` |
| Identity | ASP.NET Identity (`IdentityDbContext<User>`) |
| Frontend | Bootstrap 5, HTMX 2.0.4 (`hx-boost="true"` trên `<body>` → mọi link/form đều là HTMX), Bootstrap Icons |
| Email | `System.Net.Mail.SmtpClient` (không MailKit), config từ DB (mã hóa bằng Data Protection) |
| Tích hợp ngoài | Google Apps Script Web App (HTTP POST/GET) |
| Lệnh | `dotnet run`, `dotnet build`, `dotnet ef migrations add <Name>`, `dotnet ef database update` |
| Publish | Xem `Properties/PublishProfiles/` (FTP, Web Deploy, Zip Deploy cho Azure) |

---

## 3. Bản đồ Kiến trúc

### Areas = phân quyền

| Area | Role | Controller file | Route prefix |
|---|---|---|---|
| `Admin` | `"Admin"` | `Areas/Admin/Controllers/` | `/Admin/...` |
| `Lecturer` | `"Lecturer"` | `Areas/Lecturer/Controllers/Lecturer.cs` (class: `RegistrationController`) | `/Lecturer/Registration/...` |
| `Student` | `"Student"` | `Areas/Student/Controllers/Student.cs` (class: `RegistrationController`) | `/Student/Registration/...` |

Route Area: `[Area("X")] [Route("X/Controller/[action]/{id?}")]`.

### Lớp / Thư mục

| Lớp | Thư mục | Vai trò |
|---|---|---|
| Controllers | `Controllers/` | Tính năng chung (Room, Device, App, Grading, Topic, Home, Login) |
| Models | `Models/` | EF entities thuần — không có business logic |
| ViewModels | `ViewModels/` | ViewModel riêng cho từng view, không dùng entity trực tiếp |
| Services | `Services/` | Toàn bộ business logic |
| Data | `Data/AppDbContext.cs` | DbContext duy nhất |
| Middleware | `Middleware/ExceptionHandlingMiddleware.cs` | Xử lý lỗi tập trung |
| Helper | `Helper/GoogleSheetHelper.cs` | Parse Sheet URL |

### Pattern Controller điển hình (thin controller)
1. Resolve profile qua `GetStudentProfileIdAsync()` / `GetLecturerProfileIdAsync()` (query DB lấy ProfileId từ UserId)
2. Gọi service → nhận `(bool Success, string Message)` tuple
3. `TempData["Success"|"Error"|"Info"] = message` rồi `RedirectToAction(...)`, hoặc `return View(vm)`
4. **Không** query DB trực tiếp trong controller (ngoại lệ: load dropdown nhỏ)

---

## 4. Mô hình Dữ liệu Cốt lõi

`Data/AppDbContext.cs` — `IdentityDbContext<User>`.

| Entity | Bảng / DbSet | Ghi chú |
|---|---|---|
| `User` | AspNetUsers | Extends IdentityUser: thêm `FullName`, `Role` (string), `Mssv` (nullable, unique filtered index), `CreatedAt` |
| `StudentProfile` | StudentProfiles | 1-1 với User; `StudentCode` unique |
| `LecturerProfile` | LecturerProfiles | 1-1 với User; `LecturerCode` unique; `MaxStudents` |
| `RegistrationPeriod` | RegistrationPeriods | Đợt đăng ký: `Name`, `CourseName`, `GoogleSheetLink`, `StartDate`, `EndDate`, `IsActive`, `RestrictToAllowedStudents` |
| `Topic` | Topics | `Status`: `Open/Closed/Proposed`; FK `LecturerProfileId`, `RegistrationPeriodId`; optional `ProposedByStudentId`; `SheetRowIndex` sau sync |
| `Registration` | Registrations | Lượt đăng ký Flow A/B; `Status`: `PENDING/APPROVED/REJECTED/REVISION_REQUIRED`; composite unique `(StudentProfileId, TopicId, PeriodId)` |
| `RegistrationPeriodStudent` | RegistrationPeriodStudents | Junction: đợt ↔ SV được phép |
| `TopicRegistration` | TopicRegistrations | Đăng ký **từ Google Sheet** (Flow Sheet-Based); lưu `RowIndex`, `StudentId1/2`, `SyncStatus` |
| `TopicSyncRecord` | TopicSyncRecords | Outbox cho `TopicCreateSyncService`; denormalized data; `RetryCount` |
| `GradeRecord` | GradeRecords | Điểm; `SyncStatus` outbox cho `GradeSyncService` |
| `RoomBooking` | RoomBookings | `Status`: `"Pending"/"Approved"/"Rejected"` (string) |
| `DeviceRequest` | DeviceRequests | `Status`: `"Pending"/"Approved"/"Rejected"/"Returned"` (string) |
| `AppRegistrationRequest` | AppRegistrationRequests | `Status`: enum `Pending/Processing/Approved/Rejected` |
| `Notification` | Notifications | In-app; `UserId`, `Message`, `IsRead`, `ActionUrl` |
| `EmailConfiguration` | EmailConfigurations | SMTP config lưu DB; `EncryptedAppPassword` (Data Protection); chỉ 1 record `IsActive=true` tại 1 thời điểm |

**Cascade delete tắt (`NoAction`):** `Registration→Lecturer`, `Registration→Topic`, `Topic→ProposedByStudent`, `TopicSyncRecord→Topic`.

---

## 5. Tính năng & Nơi Chứa

| Tính năng | Controller/Service chính | View thư mục | Ghi chú workflow |
|---|---|---|---|
| **Đăng ký đề tài (MVC)** | `Services/ProjectRegistration/RegistrationService.cs` | `Areas/Student/Views/Registration/`, `Areas/Lecturer/Views/Registration/` | Flows A & B; state machine xem mục 6 |
| **Xem đề tài từ Sheet** | `Controllers/TopicController.cs` | `Views/Topic/` | Đọc Sheet → `TopicRegistration` table; độc lập với Flow A/B |
| **Quản lý đợt đăng ký** | `Areas/Admin/Controllers/RegistrationController.cs` + `IRegistrationService` | `Areas/Admin/Views/Registration/` | CRUD period, đóng đợt, export CSV |
| **Chấm điểm** | `Controllers/GradingController.cs` + `GoogleSheetsService` | `Views/Grading/` | Đọc Sheet → lưu SQL → `GradeSyncService` sync lại |
| **Đặt phòng** | `Controllers/RoomController.cs` | `Views/Room/` | `GetBookingsForCalendar` là JSON endpoint cho FullCalendar |
| **Mượn thiết bị** | `Controllers/DeviceController.cs` | `Views/Device/` | Admin duyệt → `DeviceRequest.Status` thay đổi |
| **Đăng ký phần mềm** | `Controllers/AppRegistrationController.cs` + `Services/AppRegistration/` | `Views/AppRegistration/` | Admin gán Lecturer, Lecturer approve |
| **Quản lý tài khoản** | `Areas/Admin/Controllers/AccountController.cs` + `Services/AccountManagement/AccountService.cs` | `Areas/Admin/Views/Account/` | CRUD user, tạo profile song song |
| **Cấu hình email** | `Areas/Admin/Controllers/EmailConfigController.cs` + `Services/EmailConfiguration/` | `Areas/Admin/Views/EmailConfig/` | CRUD + TestSend; mã hóa password bằng Data Protection |
| **Thông báo in-app** | `Services/NotificationSevices/NotificationService.cs` | Navbar badge (trong Layout) | `Notifications` table; đọc qua API endpoint trong Layout |
| **Xử lý lỗi** | `Middleware/ExceptionHandlingMiddleware.cs` | `Views/Shared/Error.cshtml` | Xem mục 7 & 8 |

---

## 6. State Machines

### RegistrationStatus (Flow B — đề xuất đề tài)
```
PENDING → APPROVED         (GV approve)
        → REJECTED         (GV reject, hoặc đóng đợt tự động)
        → REVISION_REQUIRED → PENDING  (SV resubmit, vòng lặp)
```

### TopicStatus
```
Open     → Closed  (hết slot hoặc proposal approve/reject)
Proposed           (SV tạo, chờ GV duyệt)
```

### Flow A (đề tài có sẵn của GV)
Student → `RegisterExistingTopicAsync` → `Registration(APPROVED)` ngay, email notify.

### Flow B (đề xuất mới)
Student ProposeNew → `Topic(Proposed)` + `Registration(PENDING)` + notify/email GV → GV Review → approve/revise/reject → notify/email SV → (nếu approve: `TopicSyncRecord` tạo để sync lên Sheet).

---

## 7. Tích hợp Ngoài & Luồng Dữ liệu

### Source-of-truth
**SQL Server** là nguồn sự thật cho tất cả dữ liệu nghiệp vụ. Google Sheet là bản ghi phản chiếu.

### Google Apps Script
- URL: `appsettings.json["GoogleAppsScript:Url"]`
- `GoogleSheetsService` (không có interface) — inject qua `AddHttpClient<GoogleSheetsService>()`, `AllowAutoRedirect=true`
- Actions: `topics` (GET, đọc DanhSachDeTai), `register` (POST, ghi SV lên Sheet), `addTopic` (POST, thêm dòng đề tài), `grade` (POST, ghi điểm)
- **Có `Console.WriteLine` debug logs** trong `GoogleSheetsService` — cân nhắc xóa khi production

### Outbox Pattern — 3 Background Workers (chạy mỗi ~1 phút)
| Service | Model | Trigger | Action |
|---|---|---|---|
| `TopicCreateSyncService` | `TopicSyncRecord` | GV tạo đề tài (approve proposal) | Ghi đề tài lên Sheet, cập nhật `Topic.SheetRowIndex` |
| `TopicSyncService` | `TopicRegistration` | SV đăng ký qua TopicController | Ghi đăng ký lên Sheet (`register` action) |
| `GradeSyncService` | `GradeRecord` | GV chấm điểm | Ghi điểm lên Sheet (`grade` action) |

`SyncStatus`: `Pending → Synced / Failed` (retry tự động). Workers dùng `IServiceScopeFactory` (Singleton hosting Scoped DbContext).

### Email — 3 lớp
```
EmailConfiguration (DB, encrypted password)
  ↓ IEmailService / EmailService         — SendAsync(to, subject, htmlBody) + legacy methods
       ↓ IEmailNotificationService        — 8 semantic methods (fail-safe, HTML templates)
            Services/EmailNotification/EmailTemplates.cs
```
- `IEmailConfigurationService` — CRUD EmailConfiguration, Admin `/Admin/EmailConfig/`
- Chỉ 1 record `IsActive=true` tại 1 thời điểm
- Password: `IDataProtector.Protect/Unprotect` với purpose `"EmailConfig.AppPassword"` — KHÔNG bao giờ trả plaintext ra client/log
- Gửi LUÔN thực hiện **sau khi DB commit** và bọc trong try-catch (lỗi chỉ log, không rollback)

### `PeriodAutoCloseService`
BackgroundService kiểm tra định kỳ, tự đóng đợt đăng ký đã quá `EndDate`, auto-reject PENDING, notify SV.

---

## 8. Quy ước & Pattern Bắt buộc

| Quy ước | Chi tiết |
|---|---|
| Async/await | Mọi I/O đều async; method name kết thúc `Async` |
| Service return | `(bool Success, string Message)` tuple khi có thể fail |
| TempData flash | Keys: `"Success"`, `"Error"`, `"Info"` — render bởi `Views/Shared/_Alerts.cshtml` |
| Toast | `TempData["SuccessMessage"]` → `Views/Shared/_SuccessToast.cshtml` (bottom-right) |
| AJAX trả JSON | Endpoint thuần AJAX trả `{ success: bool, message: string }` — KHÔNG trả HTML |
| POST protection | `[ValidateAntiForgeryToken]` trên MỌI POST action |
| Navigation properties | `null!` (nullable enabled, EF sẽ populate khi Include) |
| DateTime | `DateTime.UtcNow` cho entity fields; `DateTime.Now` cho log/sheet ghi |
| Email fail-safe | Gửi mail SAU commit DB; bọc try-catch; chỉ `LogWarning` khi lỗi, không throw |
| Thêm tính năng | Tạo ViewModel riêng, Service interface + implement, đăng ký DI Scoped trong Program.cs |
| HTMX | `hx-boost="true"` trên `<body>` — mọi link/form đi qua HTMX; 5xx không swap content, fires `htmx:responseError` |
| JS error | `handleAjaxError(message)` trong `wwwroot/js/site.js` + `htmx:responseError` handler |

---

## 9. GOTCHAS / Cạm bẫy

1. **Hai hệ thống đề tài độc lập**: `Registration`+`Topic` (MVC, Flow A/B, `RegistrationService`) vs `TopicRegistration` (Sheet-based, `TopicController`) — KHÔNG nhầm lẫn khi làm việc với từng luồng.

2. **Email chỉ gửi khi có record IsActive trong DB**: Nếu chưa cấu hình email ở `/Admin/EmailConfig`, mọi lệnh gửi mail sẽ fail (nhưng không crash nghiệp vụ vì fail-safe).

3. **App Password không được hash** (cần giải mã để gửi) — dùng Data Protection `Protect/Unprotect`, KHÔNG tự viết AES, KHÔNG log, KHÔNG trả về client.

4. **`EncryptedAppPassword` chỉ Unprotect ngay tại `CreateSmtpAsync()`** trong `EmailService` — không lưu plaintext ở bất kỳ đâu khác.

5. **HTMX không swap nội dung khi nhận 4xx/5xx** → Middleware trả 302→`/Home/Error` cho non-AJAX (HTMX follow redirect, swap body). AJAX JSON nhận 500 + `{success:false}`. Dev: re-throw để DeveloperExceptionPage xử lý.

6. **`GetBookingsForCalendar`** phải trả JSON (FullCalendar AJAX) — nếu lỗi trả `[]`, KHÔNG redirect.

7. **`User.Role` (string) và ASP.NET Identity Role song song** — khi tạo user mới phải set cả hai (`UserManager.AddToRoleAsync` + `user.Role = "..."`).

8. **`User.Mssv`** — nullable, filtered unique index (`WHERE Mssv IS NOT NULL`). Admin/Lecturer có Mssv = null.

9. **Cascade delete tắt** trên 4 quan hệ — EF migration sẽ báo lỗi cycle nếu vô tình enable cascade.

10. **`DeleteTopicAsync` throw `InvalidOperationException`** nếu đã có SV APPROVED — controller phải bắt và set TempData["Error"].

11. **`GoogleSheetsService` không có interface** — test/mock khó; inject trực tiếp class.

12. **`Console.WriteLine` debug** trong `GoogleSheetsService` — không dùng `ILogger`, cân nhắc cleanup trước production.

13. **`Areas/Lecturer/Controllers/Lecturer.cs`** — tên file ≠ tên class (class là `RegistrationController`). Tương tự `Areas/Student/Controllers/Student.cs`.

---

## 10. Bản đồ File Quan trọng

| File | Khi nào cần đọc |
|---|---|
| `Program.cs` | DI registration, middleware pipeline, cookie config |
| `Data/AppDbContext.cs` | Schema, unique constraints, cascade rules |
| `Services/ProjectRegistration/RegistrationService.cs` | Toàn bộ nghiệp vụ đề tài (flow A/B, state transitions) |
| `Services/ProjectRegistration/IRegistrationService.cs` | API surface của service đề tài |
| `Services/Email/EmailService.cs` | SMTP logic, `CreateSmtpAsync()`, `SendAsync()` |
| `Services/EmailNotification/IEmailNotificationService.cs` | 8 semantic email methods |
| `Services/EmailNotification/EmailTemplates.cs` | HTML email templates (verbatim strings) |
| `Services/GoogleSheetServices/GoogleSheetServices.cs` | HTTP calls tới AppScript |
| `Services/TopicCreateSyncServices/TopicCreateSyncService.cs` | Outbox sync đề tài mới |
| `Middleware/ExceptionHandlingMiddleware.cs` | Global error handler |
| `Views/Shared/_Layout.cshtml` | Layout chính, HTMX config, progress bar |
| `Views/Shared/_Alerts.cshtml` | TempData flash message rendering |
| `wwwroot/js/site.js` | `handleAjaxError()`, `htmx:responseError` handler |
| `AppScript.js` (root repo, ngoài project) | Google Apps Script xử lý phía Sheet |
| `Helper/GoogleSheetHelper.cs` | `ExtractSheetId(url)` |
| `Models/TopicSyncRecord.cs` | Outbox model cho tạo đề tài |
| `Areas/Admin/Controllers/EmailConfigController.cs` | CRUD cấu hình email |
| `Controllers/GradingController.cs` | Chấm điểm + Sheet integration |
| `Controllers/TopicController.cs` | Đăng ký từ Sheet (flow độc lập) |

---

## 11. Điều Chưa Chắc / Cần Xác Minh

- **`PeriodAutoCloseService` interval**: có vẻ chạy định kỳ nhưng chưa đọc kỹ thời gian — xác minh bằng cách đọc `Services/PeriodAutoClose/PeriodAutoCloseService.cs`.
- **`RegistrationPeriod.RestrictToAllowedStudents`**: logic filter SV trong `RegistrationService` có được áp dụng đầy đủ chưa hay chỉ admin quản lý danh sách? Cần đọc `RegistrationService.GetTopicListForStudentAsync`.
- **`SeedAdminController`**: có route bảo vệ bằng gì? Cần đọc trước khi deploy production.
- **`AuthService.cs`**: chưa rõ vai trò — có thể là helper cũ chưa dùng hoặc dùng trong Login flow.
- **Notification badge** trong navbar: cơ chế polling/HTMX chưa xác minh — đọc Layout phần navbar và endpoint liên quan.
