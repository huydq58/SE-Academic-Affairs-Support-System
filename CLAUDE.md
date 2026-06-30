# SE Academic Affairs Support System — CLAUDE.md

## 1. Tóm tắt

Ứng dụng web hỗ trợ công tác học vụ Khoa CNPM (SE-UIT). Ba vai trò: **Admin**, **Lecturer** (Giảng viên), **Student** (Sinh viên).

Tính năng chính:
- Đăng ký / đề xuất đề tài luận văn (quy trình phê duyệt đa bước, Flow A/B).
- Xem đề tài từ Google Sheet (luồng độc lập).
- **Chấm điểm** đồ án (nguồn SQL, đồng bộ điểm lên Google Sheets).
- **Hạn nộp báo cáo + nộp file báo cáo** (lưu local, nhắc nhở tự động qua email).
- **Đặt phòng** (FullCalendar; admin hủy slot + báo mail).
- **Mượn thiết bị theo tồn kho số lượng** (1 phiếu nhiều thiết bị; quản lý hỏng; báo cáo hư hỏng; import/export Excel).
- **Đăng ký phần mềm (CH Play)** (admin gán giảng viên xử lý + báo mail).
- Quản lý tài khoản, cấu hình email, thông báo in-app.

---

## 2. Tech Stack & Chạy

| Thứ | Chi tiết |
|---|---|
| Framework | ASP.NET Core 8 MVC, EF Core 9 |
| DB chính | SQL Server (Azure/MSSQL), key `AzureConnection` trong `appsettings.json` (**gitignored**) |
| Identity | ASP.NET Identity (`IdentityDbContext<User>`) |
| Frontend | Bootstrap 5, HTMX 2 (`hx-boost="true"` trên `<body>`), Bootstrap Icons, FullCalendar 6 |
| Excel | **ClosedXML 0.105** (đọc/ghi import/export) |
| Email | `System.Net.Mail.SmtpClient`, config lấy từ DB (mã hóa bằng Data Protection) |
| Tích hợp ngoài | Google Apps Script Web App (HTTP POST/GET) |
| Upload | Giới hạn **500MB** (Kestrel `MaxRequestBodySize` + `FormOptions`), cấu hình trong `Program.cs` |
| Lệnh | `dotnet run`, `dotnet build`, `dotnet ef migrations add <Name>`, `dotnet ef database update` |

**Lưu ý quan trọng:**
- **`Migrations/` được gitignore** (không commit). Sau khi đổi model phải tự chạy `dotnet ef migrations add` + `dotnet ef database update`.
- **`App_Data/`** (file báo cáo SV nộp) và **`DataProtectionKeys/`** và **`appsettings.json`** đều gitignored.
- File `.cshtml` được compile vào assembly → đổi view phải **build lại + restart** app mới thấy.

---

## 3. Bản đồ Kiến trúc

### Areas = phân quyền
| Area | Role | Controller | Route prefix |
|---|---|---|---|
| `Admin` | `"Admin"` | `Areas/Admin/Controllers/` | `/Admin/...` |
| `Lecturer` | `"Lecturer"` | `Areas/Lecturer/Controllers/Lecturer.cs` (class `RegistrationController`) | `/Lecturer/Registration/...` |
| `Student` | `"Student"` | `Areas/Student/Controllers/Student.cs` (class `RegistrationController`) | `/Student/Registration/...` |

Controller gốc (`Controllers/`, không area): Home, Login, Topic, **Grading**, Room, Device, AppRegistration, **Report**, SeedAdmin.

### Lớp / Thư mục
| Lớp | Thư mục | Vai trò |
|---|---|---|
| Controllers | `Controllers/`, `Areas/*/Controllers/` | Thin controller (resolve profile → gọi service → TempData → redirect) |
| Models | `Models/` | EF entities thuần |
| ViewModels | `ViewModels/` (+ vài VM trong `Models/`) | ViewModel cho view |
| Services | `Services/` | Business logic |
| Data | `Data/AppDbContext.cs` | DbContext duy nhất |
| Middleware | `Middleware/ExceptionHandlingMiddleware.cs` | Xử lý lỗi tập trung |
| Helper | `Helper/GoogleSheetHelper.cs` | Parse Sheet URL |

---

## 4. Mô hình Dữ liệu Cốt lõi

`Data/AppDbContext.cs` — `IdentityDbContext<User>`.

| Entity | Bảng | Ghi chú |
|---|---|---|
| `User` | AspNetUsers | Thêm `FullName`, `Role` (string), `Mssv` (nullable, filtered unique), `CreatedAt` |
| `StudentProfile` | StudentProfiles | 1-1 User; `StudentCode` unique |
| `LecturerProfile` | LecturerProfiles | 1-1 User; `LecturerCode` unique; `MaxStudents` |
| `RegistrationPeriod` | RegistrationPeriods | Đợt đăng ký; thêm **`ReportDeadline`** (hạn nộp báo cáo), **`LastReminderSentDate`** (chống nhắc trùng ngày), `RestrictToAllowedStudents` |
| `Topic` | Topics | `Status` Open/Closed/Proposed; FK Lecturer, Period; `SheetRowIndex` |
| `Registration` | Registrations | Flow A/B; `Status` PENDING/APPROVED/REJECTED/REVISION_REQUIRED; unique `(Student,Topic,Period)` |
| `RegistrationPeriodStudent` | — | Junction đợt ↔ SV được phép |
| `TopicRegistration` | TopicRegistrations | Đăng ký từ Google Sheet (luồng độc lập) |
| `TopicSyncRecord` | TopicSyncRecords | Outbox tạo đề tài lên Sheet |
| `GradeRecord` | GradeRecords | Điểm; outbox sync lên Sheet; key `(PeriodId, Mssv)` |
| **`ReportSubmission`** | ReportSubmissions | Bài nộp báo cáo; unique `(StudentProfileId, RegistrationPeriodId)`; file lưu **local** `App_Data/reports/{periodId}/`; FK NoAction |
| `RoomModel`, `TimeSlot` | Rooms, TimeSlots | Phòng; `Room.Condition` (Good/Broken — khác Device) |
| `RoomBooking` | RoomBookings | `Status` Pending/Approved/**Cancelled**; thêm **`CancelReason`/`CancelledAt`/`CancelledBy`** |
| **`Device`** | Devices | **Tồn kho theo số lượng**: `DeviceCode` (filtered unique), `TotalQuantity`, `BrokenQuantity`. **Đã BỎ `Status`/`Condition` cũ.** Còn lại = Total − Broken − đang mượn |
| **`DeviceRequest`** | DeviceRequests | **Header phiếu mượn** (1 phiếu nhiều thiết bị): BorrowerName/Email, Purpose, `Status` Pending/Approved/Rejected/Returned, `ReturnDate` (mức phiếu); nav `Items` |
| **`DeviceRequestItem`** | DeviceRequestItems | **Chi tiết mượn**: 1 dòng = 1 thiết bị + `Quantity`. FK Request Cascade, FK Device Restrict |
| **`DeviceDamageReport`** | DeviceDamageReports | Hư hỏng ghi nhận khi trả: snapshot DeviceName/BorrowerName + `DamagedByName`, `Quantity`, `Reason`, `ReportedAt` |
| `AppRegistrationRequest` | AppRegistrationRequests | `Status` enum Pending/Processing/Approved/Rejected; `AssignedLecturerId`; thêm **`CreatedAt`** (lọc export) |
| `Notification` | Notifications | In-app; `UserId`, `Message`, `IsRead`, `ActionUrl` |
| `EmailConfiguration` | EmailConfigurations | SMTP config; `EncryptedAppPassword` (Data Protection); chỉ 1 record `IsActive` |

**Cascade NoAction (tránh cycle):** `Registration→Lecturer/Topic`, `Topic→ProposedByStudent`, `TopicSyncRecord→Topic/Period`, `DeviceDamageReport→Request/Device`, `ReportSubmission→Student/Period`. **Restrict:** `DeviceRequestItem→Device`. **Cascade:** `DeviceRequest→Items`, `RegistrationPeriodStudent→Period/Student`.

---

## 5. Tính năng & Nơi Chứa

| Tính năng | Controller/Service | View | Ghi chú |
|---|---|---|---|
| **Đăng ký đề tài (MVC)** | `Services/ProjectRegistration/RegistrationService.cs` | `Areas/Student|Lecturer/Views/Registration/` | Flow A & B; state machine mục 6 |
| **Xem đề tài từ Sheet** | `Controllers/TopicController.cs` | `Views/Topic/` | `TopicRegistration` — độc lập Flow A/B |
| **Quản lý đợt + hạn nộp** | `Areas/Admin/Controllers/RegistrationController.cs` | `Areas/Admin/Views/Registration/` | CRUD period; đặt `ReportDeadline` → gửi mail thông báo SV (nền) |
| **Chấm điểm (SQL)** | `Controllers/GradingController.cs` | `Views/Grading/` | **Nguồn = Registration APPROVED** (không còn đọc Sheet). Lecturer chấm đề tài của mình, Admin tất cả. Điểm → `GradeRecord` → `GradeSyncService` sync lên Sheet |
| **Nộp báo cáo (SV)** | `Controllers/ReportController.cs` | `Views/Report/` | `MyReports` (landing navbar SV) → `Index?periodId` (upload). File `.doc/.docx/.pdf/.zip/.rar` ≤500MB lưu `App_Data/reports`. Nộp lại = thay thế |
| **Quản lý bài nộp (Admin)** | `ReportController.Submissions` + `DownloadAll` | `Views/Report/Submissions.cshtml` | Liệt kê SV đã nộp (lọc theo đợt) + **gom tải tất cả thành .zip** (`System.IO.Compression`, file tạm `DeleteOnClose`) |
| **Đặt phòng** | `Controllers/RoomController.cs` | `Views/Room/` | `GetBookingsForCalendar` JSON; **`WeeklySchedule`** có admin hủy slot ngay trên lịch (modal → lý do → `CancelBookingAjax` → mail). `ManageBookings` (admin list) + `CancelBooking` |
| **Mượn thiết bị** | `Controllers/DeviceController.cs` | `Views/Device/` | Tồn kho số lượng; `BorrowForm` multi-device; `ApproveRequest` kiểm tồn; `ReturnForm`/`ReturnRequest` nhận trả + ghi hư hỏng; `SetBroken`; `DamageReports`; Import/Export Excel |
| **Đăng ký phần mềm** | `Controllers/AppRegistrationController.cs` + `Services/AppRegistration/` | `Views/AppRegistration/` | Admin `AssignLecturer` → **báo GV qua in-app + email**; GV/Admin `ApproveRequest`/`RejectRequest`; `ExportExcel` |
| **Quản lý tài khoản** | `Areas/Admin/Controllers/AccountController.cs` | `Areas/Admin/Views/Account/` | CRUD user + profile |
| **Cấu hình email** | `Areas/Admin/Controllers/EmailConfigController.cs` | `Areas/Admin/Views/EmailConfig/` | CRUD + TestSend; mã hóa password |
| **Thông báo in-app** | `Services/NotificationSevices/NotificationService.cs` | Navbar badge | `Notifications` table |
| **Import/Export Excel (chung)** | `Services/Excel/ExcelService.cs` (`IExcelService`) | — | `ValidateUploadedFile` / `ReadRows` / `BuildTemplate` / `BuildWorkbook` |

### Import/Export cụ thể
- **Import đề tài (GV)**: `RegistrationService.ImportLecturerTopicsAsync` → `Areas/Lecturer/.../ImportTopics.cshtml`. Tự gán GV + đợt; chống trùng tên trong đợt; transaction + `UPDLOCK/HOLDLOCK`.
- **Import thiết bị (Admin)**: `DeviceController.ProcessDeviceImportAsync` → `Views/Device/ImportDevices.cshtml`. Cột: Mã*, Tên*, Loại, Mô tả, Link ảnh, **Tổng số lượng, Số lượng hỏng** (mỗi dòng = 1 thiết bị, dedup `DeviceCode`).
- **Export app (Excel)**: `AppRegistrationController.ExportExcel(ExportFilterViewModel)` — lọc thời gian (`CreatedAt`) + trạng thái.
- **Export thiết bị đã mượn (Excel)**: `DeviceController.ExportBorrowed(ExportFilterViewModel)` — **flatten** mỗi thiết bị 1 dòng + cột Số lượng; lọc theo `RequestDate` + đã trả/chưa trả.
- **Export hư hỏng (Excel)**: `DeviceController.ExportDamageReports`.
- **Bộ lọc export chung**: `ViewModels/ExportFilterViewModel.cs` — `ExportTimeMode {Today,ThisWeek,Range,All}` + `ResolveRange()` (giờ local). "Tuần này" = Thứ Hai→cuối ngày. Link/form export gắn `hx-boost="false"` để tải file native.

---

## 6. State Machines

### RegistrationStatus (Flow B)
```
PENDING → APPROVED (GV approve) | REJECTED (reject / đóng đợt) | REVISION_REQUIRED → PENDING (SV resubmit)
```
### TopicStatus: `Open → Closed`, `Proposed`
### Flow A: Student → `RegisterExistingTopicAsync` → Registration(APPROVED) + email.
### Flow B: Propose → Topic(Proposed)+Registration(PENDING)+notify GV → Review → approve/revise/reject.

### DeviceRequest (phiếu mượn): `Pending → Approved → Returned` | `Pending → Rejected`. Trả ở **mức phiếu** (`ReturnRequest`), kèm ghi nhận hư hỏng theo từng thiết bị (cộng vào `Device.BrokenQuantity`).
### RoomBooking: tạo trực tiếp `Approved` (không qua duyệt) → admin có thể `Cancelled` (kèm lý do + mail).
### AppRegistrationRequest: `Pending → Processing` (admin gán GV) `→ Approved | Rejected`.

---

## 7. Tích hợp Ngoài, Background Services, Email

### Google Apps Script
- URL: `appsettings.json["GoogleAppsScript:Url"]`; `GoogleSheetsService` (không interface) inject qua `AddHttpClient`.
- Actions: `topics` (GET), `register` (POST), `addTopic` (POST), `grade` (POST).
- **SQL là nguồn sự thật**; Sheet là bản phản chiếu. Chấm điểm **đọc từ SQL**, chỉ **ghi điểm** lên Sheet qua outbox.

### Background Services (`Program.cs` `AddHostedService`, dùng `IServiceScopeFactory`)
| Service | Vai trò |
|---|---|
| `TopicCreateSyncService` | Ghi đề tài mới lên Sheet (outbox `TopicSyncRecord`) |
| `TopicSyncService` | Ghi đăng ký SV (TopicController) lên Sheet |
| `GradeSyncService` | Ghi điểm (`GradeRecord`) lên Sheet |
| `PeriodAutoCloseService` | Tự đóng đợt quá hạn + auto-reject PENDING + notify |
| **`ReportDeadlineReminderService`** | Mỗi ~6h, **nhắc SV nộp báo cáo** khi còn ≤7 ngày tới `ReportDeadline` (1 lần/ngày/đợt nhờ `LastReminderSentDate`, bỏ qua SV đã nộp) |

### Email (`IEmailNotificationService` — fail-safe, gửi SAU commit, lỗi chỉ log)
`EmailNotificationService` → `EmailTemplates` (HTML, chữ ASCII). Các method:
RoomBookingConfirmed, **RoomBookingCancelled**, TopicRegistered, TopicAutoRejected, DeviceBorrowApproved, DeviceBorrowRejected, DeviceReturned, **DeviceDamaged**, AppSubmitted, AppApproved, **AppRejected**, **AppAssignedToLecturer**, **ReportDeadline** (thông báo + nhắc).
Ngoài ra `IEmailService.SendTopicProposalToLecturerAsync` (đề xuất đề tài → GV).
> Email chỉ thực gửi khi có `EmailConfiguration` `IsActive` trong DB; chưa cấu hình → log-and-skip, nghiệp vụ vẫn chạy.

---

## 8. Quy ước & Pattern Bắt buộc

| Quy ước | Chi tiết |
|---|---|
| Async/await | Mọi I/O async; tên method kết thúc `Async` |
| Service return | `(bool Success, string Message)` tuple khi có thể fail |
| TempData flash | `"Success"`/`"Error"`/`"Info"` → `Views/Shared/_Alerts.cshtml`; toast `"SuccessMessage"` |
| AJAX | trả `{ success, message }` JSON — không trả HTML |
| POST protection | `[ValidateAntiForgeryToken]` trên POST |
| HTMX | `hx-boost="true"` body → **link/form tải file phải gắn `hx-boost="false"`** (nếu không HTMX nuốt response, file không tải được) |
| Email fail-safe | gửi SAU commit, bọc try-catch, chỉ LogWarning |
| Thêm tính năng | ViewModel riêng, Service interface + DI Scoped trong `Program.cs` |
| Migration | scaffold + `database update` thủ công (Migrations gitignored) |

---

## 9. GOTCHAS / Cạm bẫy

1. **Hai hệ thống đề tài độc lập**: `Registration`+`Topic` (MVC) vs `TopicRegistration` (Sheet). Không nhầm.
2. **Chấm điểm = SQL** (`Registration` APPROVED), **không** còn đọc Google Sheet. Chỉ topic có SV được duyệt mới hiện.
3. **Device không còn `Status`/`Condition`** — dùng `TotalQuantity`/`BrokenQuantity`; "còn lại" tính động = Total − Broken − (Σ Quantity item thuộc phiếu Approved). Mọi nơi tham chiếu `Device.Status`/`Condition` đều đã bỏ.
4. **Phiếu mượn header/detail**: `DeviceRequest` (header) + `DeviceRequestItem` (1 thiết bị + Quantity). Trả ở **mức phiếu**. Duyệt phải kiểm tồn kho từng thiết bị.
5. **Hư hỏng**: ghi `DeviceDamageReport` + cộng `BrokenQuantity` khi nhận trả (`ReturnForm`/`ReturnRequest`).
6. **HTMX nuốt file download** → link/form export/template/zip phải `hx-boost="false"`.
7. **Migrations + appsettings.json + App_Data + DataProtectionKeys đều gitignored.** DB phải `database update` thủ công.
8. **Upload 500MB** cần Kestrel + FormOptions (đã set `Program.cs`) + `[RequestSizeLimit]`/`[RequestFormLimits]` trên action upload.
9. **File báo cáo lưu local** `App_Data/reports/{periodId}/` (không web-accessible) — tải qua `ReportController.Download`/`DownloadAll` (PhysicalFile / zip `DeleteOnClose`).
10. **`DeviceController` chưa có `[Authorize]` cấp class** — các action import/export/damage gắn `[Authorize(Roles="Admin")]` riêng; CRUD/borrow cũ vẫn để ngỏ (nợ kỹ thuật).
11. **`User.Role` (string) song song Identity Role** — tạo user phải set cả hai.
12. **`Areas/{Lecturer,Student}/Controllers/*.cs`** — tên file ≠ tên class (đều là `RegistrationController`).
13. **`GoogleSheetsService` không interface, có `Console.WriteLine` debug.**
14. **Hủy đặt phòng** = soft-cancel (`Status="Cancelled"` + lý do) → slot biến khỏi lịch (chỉ hiện Approved), vẫn lưu để xử lý sau.
15. **`DeviceCode`/`Mssv`/`ReportSubmission(Student,Period)`** dùng filtered/unique index — coi chừng khi seed/import trùng.

---

## 10. Bản đồ File Quan trọng

| File | Khi nào cần đọc |
|---|---|
| `Program.cs` | DI, hosted services, **Kestrel/FormOptions 500MB**, pipeline |
| `Data/AppDbContext.cs` | Schema, unique constraints, cascade |
| `Services/ProjectRegistration/RegistrationService.cs` | Nghiệp vụ đề tài + import GV + announce hạn nộp |
| `Controllers/GradingController.cs` | Chấm điểm từ SQL |
| `Controllers/ReportController.cs` | Nộp/tải báo cáo + admin gom zip |
| `Services/ReportDeadline/ReportDeadlineReminderService.cs` | Nhắc hạn nộp hằng ngày |
| `Controllers/DeviceController.cs` | Tồn kho, mượn/trả, hư hỏng, import/export |
| `Controllers/RoomController.cs` | Đặt phòng + hủy (form + AJAX calendar) |
| `Services/Excel/ExcelService.cs` | Đọc/ghi .xlsx dùng chung |
| `Services/EmailNotification/{IEmailNotificationService,EmailTemplates}.cs` | Các loại mail tự động |
| `Services/GoogleSheetServices/GoogleSheetServices.cs` | HTTP tới AppScript |
| `Middleware/ExceptionHandlingMiddleware.cs` | Global error handler |
| `Views/Shared/_Layout.cshtml` | Navbar (dropdown theo role), HTMX config |
| `ViewModels/ExportFilterViewModel.cs` | Bộ lọc export chung |

---

## 11. Điều Chưa Chắc / Cần Xác Minh

- `GoogleSheetsService` còn `Console.WriteLine` debug — cân nhắc xóa trước production.
- `DeviceController` thiếu `[Authorize]` cấp class — nên siết quyền các action CRUD/borrow cũ.
- `SeedAdminController` — kiểm tra bảo vệ route trước khi deploy.
- `AuthService.cs` — vai trò chưa rõ (có thể helper cũ).
- Zip "Tải tất cả" tạo file tạm ở `Path.GetTempPath()` + `DeleteOnClose` — với đợt rất nhiều file lớn cần theo dõi dung lượng đĩa tạm.
