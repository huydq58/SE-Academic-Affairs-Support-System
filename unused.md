# Audit: Unused / Duplicate / Dead Code

> Được tạo ngày 2026-06-12 bằng cách scan toàn bộ project.

---

## 1. CSS Classes trùng lặp (định nghĩa ở 2+ file)

Những class này được copy-paste vào `<style>` của từng view riêng lẻ. Nên gộp vào `wwwroot/css/site.css` để tránh bảo trì nhiều nơi.

### Trùng lặp nhiều nhất (≥ 5 file)

| Class | Số file | Danh sách file |
|-------|---------|----------------|
| `.rg-page-title` | 11 | Areas/Admin/Views/Registration/CreatePeriod, Periods; Areas/Lecturer/Views/Registration/ActivePeriods, CreateTopic, Inbox, MyTopics, Review; Areas/Student/Views/Registration/ActivePeriods, MyRegistrations, ProposeNew, TopicList |
| `.sb` | 10 | Areas/Admin/Periods; Areas/Lecturer/Inbox, MyTopics, Review; Areas/Student/MyRegistrations, TopicList; Views/Device/BorrowRequests, Catalog, Edit, Index |
| `.sb-green` | 9 | Areas/Admin/Periods; Areas/Lecturer/Inbox, MyTopics; Areas/Student/MyRegistrations, TopicList; Views/Device/BorrowRequests, Catalog, Edit, Index |
| `.sb-red` | 7 | Areas/Lecturer/Inbox, MyTopics; Areas/Student/MyRegistrations; Views/Device/BorrowRequests, Catalog, Edit, Index |
| `.sb-blue` | 7 | Areas/Lecturer/Inbox, MyTopics, Review; Areas/Student/MyRegistrations; Views/Device/Catalog, Edit, Index |
| `.sb-amber` | 7 | Areas/Admin/Periods; Areas/Lecturer/Inbox, MyTopics; Areas/Student/MyRegistrations; Views/Device/BorrowRequests, Edit, Index |
| `.sb-gray` | 6 | Areas/Admin/Periods; Areas/Lecturer/Inbox, MyTopics; Areas/Student/MyRegistrations, TopicList; Views/Device/BorrowRequests |
| `.dt-wrap` | 6 | Areas/Admin/Periods; Areas/Lecturer/Inbox, MyTopics; Views/Device/Catalog, Index; Views/Room/PendingBookings |
| `.dt` | 6 | Areas/Admin/Periods; Areas/Lecturer/Inbox, MyTopics; Views/Device/Catalog, Index; Views/Room/PendingBookings |
| `.dv-page-title` | 6 | Views/Device/BorrowForm, BorrowRequests, Catalog, Create, Edit, Index |
| `.stat-box` | 5 | Areas/Lecturer/Inbox; Views/AppRegistration/PendingRequests; Views/Device/BorrowRequests, Index; Views/Room/PendingBookings |
| `.stat-num` | 5 | (như trên) |
| `.stat-lbl` | 5 | (như trên) |
| `.sn-amber` | 4 | Areas/Lecturer/Inbox; Views/Device/BorrowRequests, Index; Views/Room/PendingBookings |
| `.btn-act` | 4 | Areas/Admin/Periods; Views/Device/BorrowRequests, Index; Views/Room/PendingBookings |
| `.btn-act-danger` | 4 | Areas/Admin/Periods; Views/Device/BorrowRequests, Index; Views/Room/PendingBookings |
| `.rm-page-title` | 4 | Views/Home/RoomBooking, RoomList; Views/Room/PendingBookings, WeeklySchedule |

### Trùng lặp vừa (2–3 file)

| Class | Số file |
|-------|---------|
| `.rg-form-card`, `.rg-form-head`, `.rg-form-body` | 3 (CreatePeriod, CreateTopic, ProposeNew) |
| `.btn-act-success` | 3 (Review, BorrowRequests, PendingBookings) |
| `.sn-green`, `.sn-blue` | 3 |
| `.period-card`, `.period-card-head`, `.period-card-body` | 2 (Student/ActivePeriods, Lecturer/ActivePeriods) |
| `.btn-period-primary`, `.btn-period-outline` | 2 (Student/ActivePeriods, Lecturer/ActivePeriods) |
| `.btn-borrow`, `.btn-borrow-disabled` | 2 (Catalog, BorrowForm) |
| `.cat-tag`, `.tech-tag` | 2 |
| `.info-row`, `.info-lbl`, `.info-val` | 2 |
| `.decision-opts` | 2 |
| `.empty-state`, `.empty-state-box` | 2 |
| `.section-head` | 2 |
| `.schedule-wrap` | 2 |
| `.search-wrap` | 2 |
| `.hint-desktop`, `.hint-mobile` | 2 |
| `.feature-grid` | 2 |
| `.dv-section`, `.dv-section-head`, `.dv-section-body`, `.dv-footer`, `.dv-wrap` | 2 |

**Khuyến nghị**: Tách toàn bộ nhóm `.sb-*`, `.dt-*`, `.stat-*`, `.rg-page-title`, `.btn-act-*`, `.sn-*` ra file `wwwroot/css/shared-components.css` và import vào `_Layout.cshtml`.

---

## 2. CSS Classes định nghĩa nhưng không dùng trong HTML (file-local)

Sau khi kiểm tra từng file, **không tìm thấy CSS class nào được định nghĩa mà không dùng** trong markup của cùng file đó.

---

## 3. C# Models không được sử dụng

| Class | File | Vấn đề |
|-------|------|--------|
| `LoginViewModel` | `Models/LoginViewModel.cs` | **UNUSED** — Định nghĩa đủ `Email`, `Password`, `RememberMe` nhưng `LoginController` không dùng; controller nhận tham số rời thay vì bind vào model này. |
| `GradingSheet` | `Models/GradingSheet.cs` | **Partial UNUSED** — Class wrapper `GradingSheet` không được tham chiếu ở đâu. Chỉ `GradingSheetRow` (nested class bên trong) được dùng trong ViewModel và Service. |

---

## 4. C# ViewModels không được sử dụng (container class)

Các file ViewModel dưới đây dùng pattern "container class chứa nested classes". Bản thân container class **không bao giờ được tham chiếu** — chỉ các nested class con mới được dùng làm `@model` trong Views.

| Container Class | File | Nested classes thực sự dùng |
|----------------|------|------------------------------|
| `StudentViewModel` | `ViewModels/StudentViewModel.cs` | `TopicListViewModel`, `TopicCardViewModel`, `LecturerSelectItem`, `ProposalViewModel`, `MyRegistrationsViewModel`, `RegistrationRowViewModel` |
| `AdminViewModel` | `ViewModels/AdminViewModel.cs` | `PeriodFormViewModel`, `ExportRowViewModel` |
| `LecturerViewModel` | `ViewModels/LecturerViewModel.cs` | `LecturerInboxViewModel`, `ProposalReviewItem`, `ReviewDecisionViewModel`, `LecturerTopicsViewModel`, `TopicManageRow`, `CreateTopicViewModel` |
| `GradingViewModel` | `ViewModels/GradingViewModel.cs` | `GradingListViewModel`, `GradeFormViewModel` |

**Khuyến nghị**: Xóa các container class hoặc giữ nguyên như namespace group — không ảnh hưởng runtime nhưng gây nhầm lẫn khi đọc code.

---

## 5. Controller Actions không được sử dụng

| Controller | Action | File | Vấn đề |
|-----------|--------|------|--------|
| `HomeController` | `login()` | `Controllers/HomeController.cs` | **UNUSED** — Action này không được route nào, view nào, hay `asp-action` nào tham chiếu. Logic đăng nhập đã nằm hết trong `LoginController`. |

---

## 6. Lỗi tên file (typo)

| File hiện tại | Nên đặt tên |
|---------------|-------------|
| `Services/AppRegistration/IAppRegistraionService.cs` | `IAppRegistrationService.cs` |

> Tên interface bên trong vẫn đúng (`IAppRegistrationService`) nhưng tên file thiếu chữ `t`: `Registr**a**ion` → `Registration`.

---

## 7. Services — Nhận xét

Tất cả public method trong các Service đều có caller:
- `AppRegistrationService` → được gọi từ `AppRegistrationController`
- `EmailService` → 3 method đều được gọi từ controller tương ứng
- `GoogleSheetsService` → được gọi từ `GradingController`, `TopicController`
- `NotificationService`, `RegistrationService` → đều có controller gọi

Không tìm thấy method thừa rõ ràng ở tầng Service.

---

## Tóm tắt action items

| # | Loại | Mức ưu tiên | Hành động |
|---|------|-------------|-----------|
| 1 | CSS trùng lặp | Cao | Tách `.sb-*`, `.dt-*`, `.stat-*`, `.rg-page-title`, `.btn-act-*` ra `shared-components.css` |
| 2 | `LoginViewModel` | Thấp | Xóa file hoặc dùng làm model thực sự trong LoginController |
| 3 | `GradingSheet` wrapper | Thấp | Xóa class wrapper, giữ `GradingSheetRow` |
| 4 | Container ViewModels (4 file) | Thấp | Xóa container class, để nested class ra file riêng hoặc giữ nguyên namespace |
| 5 | `HomeController.login()` | Thấp | Xóa action thừa |
| 6 | Typo tên file | Thấp | Rename `IAppRegistraionService.cs` → `IAppRegistrationService.cs` |
