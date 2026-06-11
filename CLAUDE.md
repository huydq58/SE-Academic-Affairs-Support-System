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
│   ├── Admin/Controllers/Admin.cs          # Quản lý đợt đăng ký, xem toàn bộ dữ liệu
│   ├── Lecturer/Controllers/Lecturer.cs    # GV: tạo đề tài, duyệt đề xuất, chấm điểm
│   └── Student/Controllers/Student.cs      # SV: đăng ký, đề xuất đề tài, xem kết quả
├── Controllers/
│   ├── HomeController.cs                   # Trang chủ, đặt phòng
│   ├── LoginController.cs                  # Đăng nhập / đăng xuất
│   ├── TopicController.cs                  # API phụ trợ cho đề tài
│   ├── GradingController.cs                # Chấm điểm (tích hợp Google Sheets)
│   ├── DeviceController.cs                 # Mượn thiết bị
│   ├── RoomController.cs                   # Đặt phòng
│   └── AppRegistrationController.cs        # Đăng ký phần mềm
├── Models/                                 # EF Core entities thuần (không có logic)
├── ViewModels/                             # ViewModel riêng cho từng view
├── Services/
│   ├── ProjectRegistration/
│   │   ├── IRegistrationService.cs
│   │   └── RegistrationService.cs          # Toàn bộ nghiệp vụ đăng ký đề tài
│   ├── GoogleSheetServices/GoogleSheetServices.cs  # HTTP client gọi Google Apps Script
│   ├── TopicSyncServices/TopicSyncService.cs       # BackgroundService đồng bộ đăng ký
│   ├── GradeSyncService/GradeSyncService.cs        # BackgroundService đồng bộ điểm
│   ├── NotificationSevices/NotificationService.cs  # Thông báo in-app
│   └── AppRegistration/AppRegistrationService.cs
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
  → Notify GV

GV xem Inbox → Review(id)
  → POST Review(decision: "approve"|"revise"|"reject")
    → approve: Registration=APPROVED, Topic=Closed, notify SV
    → revise:  Registration=REVISION_REQUIRED, notify SV với note
    → reject:  Registration=REJECTED, Topic=Closed, notify SV

SV ReviseProposal(id)  [chỉ khi Status=REVISION_REQUIRED]
  → Cập nhật Topic + Registration, tăng RevisionCount
  → Status trở về PENDING, notify GV
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
AddScoped<IAppRegistrationService, AppRegistrationService>()
AddScoped<INotificationService, NotificationService>()
AddScoped<IRegistrationService, RegistrationService>()

// Singleton background services
AddHostedService<GradeSyncService>()
AddHostedService<TopicSyncService>()

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

## Các file cần đọc đầu tiên khi làm việc với tính năng

| Tính năng | File chính |
|---|---|
| Đăng ký đề tài (toàn bộ) | `Services/ProjectRegistration/RegistrationService.cs` |
| Model quan hệ | `Data/AppDbContext.cs`, `Models/Topic.cs`, `Models/Registration.cs`, `Models/RegistrationPeriod.cs` |
| Google Sheets sync | `Services/GoogleSheetServices/GoogleSheetServices.cs`, `Services/TopicSyncServices/TopicSyncService.cs`, `Services/GradeSyncService/GradeSyncService.cs` |
| Auth & routing | `Program.cs`, `Areas/*/Controllers/*.cs` |
| Chấm điểm | `Controllers/GradingController.cs`, `Models/GradeRecord.cs`, `Models/GradingSheet.cs` |
| Đặt phòng | `Controllers/RoomController.cs`, `Models/RoomBooking.cs`, `Models/TimeSlot.cs` |
