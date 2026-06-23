# SE Academic Affairs Support System — CLAUDE.md

## Tổng quan hệ thống

Ứng dụng web hỗ trợ công tác học vụ cho một trường đại học Việt Nam, xây dựng bằng **ASP.NET Core 8 MVC** + **Entity Framework Core 9** + **SQL Server (Azure)**. Giao diện tiếng Việt, backend tiếng Anh.

Các tính năng chính:
- **Đăng ký đề tài** (thesis/project registration) — luồng nghiệp vụ cốt lõi
- Đặt phòng học (Room Booking)
- Mượn thiết bị (Device Borrowing)
- Yêu cầu đăng ký phần mềm (App Registration)
- Chấm điểm và đồng bộ lên Google Sheets

---

## Cấu trúc thư mục

```
SE Academic Affairs Support System/
├── Areas/
│   ├── Admin/
│   │   ├── Controllers/
│   │   │   ├── AccountController.cs        # Quản lý tài khoản (CRUD user)
│   │   │   └── RegistrationController.cs   # Quản lý đợt đăng ký đề tài
│   │   └── Views/
│   │       ├── Account/Index.cshtml, CreateEdit.cshtml
│   │       └── Registration/Periods.cshtml, CreatePeriod.cshtml
│   ├── Lecturer/
│   │   ├── Controllers/Lecturer.cs         # GV: tạo đề tài, duyệt đề xuất, chấm điểm
│   │   └── Views/Registration/
│   │       ├── Inbox.cshtml, Review.cshtml
│   │       ├── MyTopics.cshtml, CreateTopic.cshtml, ActivePeriods.cshtml
│   └── Student/
│       ├── Controllers/Student.cs          # SV: đăng ký, đề xuất đề tài, xem kết quả
│       └── Views/Registration/
│           ├── TopicList.cshtml, ProposeNew.cshtml, ReviseProposal.cshtml
│           ├── MyRegistrations.cshtml, ActivePeriods.cshtml, NoPeriod.cshtml
├── Controllers/
│   ├── HomeController.cs                   # Trang chủ (dashboard theo role)
│   ├── LoginController.cs                  # Đăng nhập / đăng xuất
│   ├── TopicController.cs                  # Xem danh sách đề tài từ Google Sheet
│   ├── GradingController.cs                # Chấm điểm (tích hợp Google Sheets)
│   ├── DeviceController.cs                 # Mượn thiết bị (CRUD + duyệt yêu cầu)
│   ├── RoomController.cs                   # Đặt phòng học
│   ├── AppRegistrationController.cs        # Đăng ký phần mềm
│   └── SeedAdminController.cs             # Tạo tài khoản test (dev only)
├── Models/                                 # EF Core entities thuần (không có logic)
├── ViewModels/                             # ViewModel riêng cho từng view
│   ├── StudentViewModel.cs                 # ViewModels cho luồng Student
│   ├── LecturerViewModel.cs                # ViewModels cho luồng Lecturer
│   ├── GradingViewModel.cs                 # ViewModels cho chấm điểm
│   ├── AdminViewModel.cs                   # ViewModels cho Admin
│   └── AccountViewModel.cs                 # UserListViewModel, UserFormViewModel
├── Services/
│   ├── ProjectRegistration/
│   │   ├── IRegistrationService.cs
│   │   ├── RegistrationService.cs          # Toàn bộ nghiệp vụ đăng ký đề tài
│   │   ├── IRegistrationPeriodStudentService.cs
│   │   └── RegistrationPeriodStudentService.cs  # Quản lý SV trong đợt đăng ký
│   ├── AccountManagement/
│   │   ├── IAccountService.cs
│   │   └── AccountService.cs               # CRUD tài khoản (Admin)
│   ├── Email/
│   │   ├── IEmailService.cs
│   │   └── EmailService.cs                 # Gửi email thông báo
│   ├── GoogleSheetServices/GoogleSheetServices.cs  # HTTP client gọi Google Apps Script
│   ├── TopicSyncServices/TopicSyncService.cs       # BackgroundService đồng bộ đăng ký
│   ├── GradeSyncService/GradeSyncService.cs        # BackgroundService đồng bộ điểm
│   ├── PeriodAutoClose/PeriodAutoCloseService.cs   # BackgroundService tự đóng đợt hết hạn
│   ├── NotificationSevices/NotificationService.cs  # Thông báo in-app
│   ├── AppRegistration/AppRegistrationService.cs
│   └── AuthService.cs                     # Helper xác thực
├── Data/AppDbContext.cs                    # DbContext duy nhất
└── Program.cs                             # DI, middleware, routing setup
```

---

## Kiến trúc & Patterns

### Areas = phân quyền theo vai trò
Ba Area tương ứng ba vai trò, mỗi Area có controller + views riêng:

| Area | Role | Attribute |
|---|---|---|
| `Admin` | `"Admin"` | `[Authorize(Roles = "Admin")]` |
| `Lecturer` | `"Lecturer"` | `[Authorize(Roles = "Lecturer")]` |
| `Student` | `"Student"` | `[Authorize(Roles = "Student")]` |

Route pattern của Area: `{area}/{controller=Home}/{action=Index}/{id?}`  
Ví dụ: `GET /Student/Registration/TopicList`

### Thin controller, fat service
Controller chỉ làm 3 việc:
1. Resolve profile của user hiện tại qua `GetStudentProfileIdAsync()` / `GetLecturerProfileIdAsync()`
2. Gọi service
3. Set `TempData["Success"|"Error"|"Info"]` rồi redirect, hoặc trả về View với ViewModel

Toàn bộ business logic nằm trong `RegistrationService` (implement `IRegistrationService`).

### Identity + Role string
`User : IdentityUser` có thêm `FullName`, `Role` (string), `CreatedAt`.  
Hệ thống dùng **ASP.NET Identity** với role-based authorization. Role được lưu trong bảng `AspNetRoles`.  
`User.Role` (string field) và Identity role song song nhau — đây là điều cần chú ý khi thêm user mới.

### ViewModel pattern
Views không nhận entity trực tiếp. Mỗi view có ViewModel riêng trong `ViewModels/`.  
Ví dụ: `TopicListViewModel`, `ProposalViewModel`, `ReviewDecisionViewModel`, `GradingViewModel`.

### TempData flash messages
Feedback ngắn gọn dùng `TempData["Success"]`, `TempData["Error"]`, `TempData["Info"]`.  
Rendered bởi partial `Views/Shared/_Alerts.cshtml`.

---

## Domain Model & Quan hệ chính

```
RegistrationPeriod (đợt đăng ký)
  └── Topic[] (đề tài)
        ├── LecturerProfile (chủ sở hữu đề tài)
        ├── StudentProfile? (người đề xuất, chỉ có ở Flow B)
        └── Registration[] (lượt đăng ký)
              ├── StudentProfile
              ├── LecturerProfile
              └── RegistrationPeriod

User (Identity)
  ├── StudentProfile (1-1, UserId FK)
  └── LecturerProfile (1-1, UserId FK)
```

**Unique constraints quan trọng (trong `OnModelCreating`):**
- `StudentProfile.StudentCode` — unique
- `LecturerProfile.LecturerCode` — unique
- `Registration(StudentProfileId, TopicId, RegistrationPeriodId)` — composite unique
- `User.Email` — unique

**Cascade delete được tắt (`NoAction`)** cho các quan hệ:
- `Registration → Lecturer`
- `Registration → Topic`
- `Topic → ProposedByStudent`

---

## Luồng đăng ký đề tài (nghiệp vụ cốt lõi)

### Flow A — Đăng ký đề tài có sẵn (của GV)
```
Student xem TopicList (Open topics)
  → POST Register(topicId)
    → Kiểm tra: slot còn, chưa đăng ký, chưa có đề tài approved trong kỳ
    → Tạo Registration(Status=APPROVED) ngay lập tức
    → Nếu hết slot → Topic.Status = Closed
```

### Flow B — Đề xuất đề tài mới
```
Student POST ProposeNew(vm)
  → Tạo Topic(Status=Proposed) + Registration(Status=PENDING)
  → Notify GV (in-app notification + email qua IEmailService)
  → Email: SendTopicProposalToLecturerAsync — bắt exception, log warning nếu thất bại

GV xem Inbox → MyTopics (hiển thị badge pending count) → Review(id)
  → POST Review(decision: "approve"|"revise"|"reject")
    → approve: Registration=APPROVED, Topic=Closed, notify SV, email SV, tạo TopicSyncRecord
    → revise:  Registration=REVISION_REQUIRED, notify SV với note, email SV (bắt buộc note)
    → reject:  Registration=REJECTED, Topic=Closed, notify SV, email SV
  → Email: SendTopicDecisionToStudentAsync — bắt exception, log warning nếu thất bại

SV ReviseProposal(id)  [chỉ khi Status=REVISION_REQUIRED]
  → Cập nhật Topic + Registration, tăng RevisionCount
  → Status trở về PENDING, notify GV + email GV
```

### State machine `RegistrationStatus`
```
PENDING → APPROVED
        → REJECTED
        → REVISION_REQUIRED → PENDING (lặp lại)
```

### State machine `TopicStatus`
```
Open → Closed (hết slot hoặc khi proposal được approve/reject)
Proposed (topic do SV tạo, chờ GV duyệt)
```

---

## Google Sheets Integration

Kiến trúc: **Outbox pattern** — ghi DB trước, background worker đồng bộ sau.

### `SyncStatus` enum
```
Pending → Synced
        → Failed (worker sẽ retry ở lần chạy tiếp theo)
```

### Hai background workers (chạy mỗi 1 phút)
| Service | Model | Action |
|---|---|---|
| `TopicSyncService` | `TopicRegistration` | Ghi đăng ký lên sheet (action=`register`) |
| `GradeSyncService` | `GradeRecord` | Ghi điểm lên sheet (action=`grade`) |

### `GoogleSheetsService`
Gọi một **Google Apps Script Web App** URL (cấu hình trong `appsettings.json["GoogleAppsScript:Url"]`).  
Dùng `HttpClient` được inject qua `AddHttpClient<GoogleSheetsService>()`.  
Background workers dùng `IServiceScopeFactory` để tạo scope riêng (vì `BackgroundService` là Singleton nhưng `DbContext` là Scoped).

### `RegistrationPeriod.GoogleSheetLink`
Mỗi đợt đăng ký có thể gắn một Google Sheet URL.  
`GetDownloadLink(format)` parse URL để tạo link export (xlsx/csv).  
`GoogleSheetHelper.ExtractSheetId(url)` tách Sheet ID từ URL để truyền cho Apps Script.

---

## Database & Migrations

- Primary DB: **SQL Server** (`AzureConnection` trong `appsettings.json`)
- Backup config: **PostgreSQL** (`DefaultConnection`) — đã comment out trong `Program.cs`
- Migration files: `Migrations/` — dùng `dotnet ef migrations add` / `dotnet ef database update`

---

## DI Registration (Program.cs)

```csharp
// Scoped services
AddScoped<IEmailService, EmailService>()            // email thông báo
AddScoped<IAppRegistrationService, AppRegistrationService>()
AddScoped<INotificationService, NotificationService>()
AddScoped<IRegistrationService, RegistrationService>()
AddScoped<IAccountService, AccountService>()
AddScoped<IRegistrationPeriodStudentService, RegistrationPeriodStudentService>()

// Singleton background services
AddHostedService<GradeSyncService>()
AddHostedService<TopicSyncService>()
AddHostedService<TopicCreateSyncService>()  // đồng bộ tạo đề tài lên Google Sheet
AddHostedService<PeriodAutoCloseService>()  // tự đóng đợt khi quá EndDate

// HttpClient
AddHttpClient<GoogleSheetsService>()

// Identity
AddIdentity<User, IdentityRole>()
  .AddEntityFrameworkStores<AppDbContext>()
  .AddDefaultTokenProviders()
```

Cookie auth: 8 giờ, sliding expiration. Login redirect về `/Home/Index`.

---

## Business Rules quan trọng

1. Một SV chỉ có **một đề tài APPROVED** mỗi đợt đăng ký.
2. Một SV không thể có đề xuất PENDING đang chờ đồng thời với một đề xuất khác.
3. Đề tài không thể xóa nếu đã có SV được duyệt (`DeleteTopicAsync` throw `InvalidOperationException`).
4. Khi đóng đợt đăng ký (`ClosePeriodAndAutoRejectPendingAsync`), tất cả Registration PENDING bị tự động REJECTED và SV được notify.
5. SV chỉ có thể hủy Registration khi Status là PENDING hoặc REVISION_REQUIRED (không hủy được APPROVED hoặc REJECTED).
6. GV muốn yêu cầu revise **bắt buộc** phải kèm note (`vm.Note` không được rỗng).

---

## Conventions trong code

- **Async/await** xuyên suốt, method name kết thúc bằng `Async`.
- Service methods trả về `(bool Success, string Message)` tuple khi có thể fail.
- `null!` được dùng cho navigation properties (nullable enabled, EF sẽ populate).
- `DateTime.UtcNow` cho `CreatedAt`, `UpdatedAt` trong entities; `DateTime.Now` trong một số chỗ khi ghi log/sheet.
- `TempData` keys: `"Success"`, `"Error"`, `"Info"` — nhất quán toàn app.
- `[ValidateAntiForgeryToken]` trên mọi POST action.
- Controller không trực tiếp query DB — phải qua service (ngoại lệ: một số query lookup nhỏ trong controller như load danh sách lecturer để fill dropdown).

---

## Email Service

`IEmailService` / `EmailService` — đăng ký DI là `Scoped`. SMTP: Gmail hardcoded (app password).

Các method hiện có:
| Method | Mục đích |
|---|---|
| `SendConfirmRoomAsync` | Xác nhận đặt phòng |
| `SendConfirmDeviceAsync` | Xác nhận mượn thiết bị |
| `SendConfirmAppAsync` | Xác nhận đăng ký phần mềm |
| `SendTopicProposalToLecturerAsync` | Thông báo GV khi SV gửi đề xuất đề tài (trả `Task<bool>`) |
| `SendTopicDecisionToStudentAsync` | Thông báo SV khi GV duyệt/từ chối/yêu cầu sửa (trả `Task<bool>`) |

`TopicDecisionType` enum: `Approve`, `Revise`, `Reject` — dùng cho `SendTopicDecisionToStudentAsync`.

**Gotcha:** Các method cũ (Room/Device/App) được gọi với `new EmailService(config)` trực tiếp trong controller — chưa dùng DI. Chỉ 2 method topic mới dùng DI qua `RegistrationService`. Khi refactor, hãy chuyển các controller đó sang inject `IEmailService`.

**Email failure handling:** Trong `RegistrationService`, mọi email gọi đều được bọc trong try-catch, log `ILogger.LogWarning` nếu thất bại, không làm fail nghiệp vụ DB.

---

## Google Sheets — Cấu trúc sheet DanhSachDeTai

Sheet "DanhSachDeTai" (cột 1-indexed):
```
A: STT | B: TopicId (DB) | C: Tên đề tài | D: Mô tả | E: Yêu cầu đầu vào
F: Công nghệ | G: Số SV tối đa | H: Tên GV | I: Mã GV | J: MSSV SV | K: Tên SV | L: Ghi chú
```

AppScript (`AppScript.js` ở root repo) sử dụng dynamic column detection (`buildColMap` / `findColIdx`) để chịu được sheet cũ và mới.

`Topic.SheetRowIndex` lưu vị trí dòng trên sheet sau khi sync thành công. Dùng để cập nhật sheet khi SV đăng ký (gọi `registerStudent` action).

`TopicCreateSyncService` — background worker đồng bộ đề tài mới lên sheet (outbox: `TopicSyncRecords` table). Sau khi sync thành công, cập nhật `Topic.SheetRowIndex` trong DB.

---

## Các file cần đọc đầu tiên khi làm việc với tính năng

| Tính năng | File chính |
|---|---|
| Đăng ký đề tài (toàn bộ) | `Services/ProjectRegistration/RegistrationService.cs` |
| Model quan hệ | `Data/AppDbContext.cs`, `Models/Topic.cs`, `Models/Registration.cs`, `Models/RegistrationPeriod.cs` |
| Google Sheets sync | `Services/GoogleSheetServices/GoogleSheetServices.cs`, `Services/TopicSyncServices/TopicSyncService.cs`, `Services/GradeSyncService/GradeSyncService.cs` |
| Google Apps Script | `AppScript.js` (root repo, ngoài project folder) |
| Auth & routing | `Program.cs`, `Areas/*/Controllers/*.cs` |
| Chấm điểm | `Controllers/GradingController.cs`, `Models/GradeRecord.cs`, `Models/GradingSheet.cs` |
| Đặt phòng | `Controllers/RoomController.cs`, `Models/RoomBooking.cs`, `Models/TimeSlot.cs` |
| Quản lý tài khoản | `Areas/Admin/Controllers/AccountController.cs`, `Services/AccountManagement/AccountService.cs`, `ViewModels/AccountViewModel.cs` |
| Quản lý đợt đăng ký | `Areas/Admin/Controllers/RegistrationController.cs`, `Models/RegistrationPeriod.cs` |
| Mượn thiết bị | `Controllers/DeviceController.cs`, `Models/Device.cs`, `Models/DeviceRequest.cs` |
| Đăng ký phần mềm | `Controllers/AppRegistrationController.cs`, `Services/AppRegistration/AppRegistrationService.cs`, `Models/AppRegistrationRequest.cs` |
| Email thông báo đề tài | `Services/Email/IEmailService.cs`, `Services/Email/EmailService.cs` |
