# Danh sách View & Tính năng — SE Academic Affairs Support System

Tài liệu này liệt kê toàn bộ màn hình (`.cshtml`) trong hệ thống, phân theo nhóm chức năng, kèm mô tả tính năng và URL tương ứng.

---

## 1. Màn hình chung (không cần đăng nhập / mọi role)

### `Views/Login/Index.cshtml`
**URL:** `GET /Login`  
**Tính năng:**
- Form đăng nhập với username, password, checkbox "Ghi nhớ đăng nhập"
- Redirect về trang chủ sau khi đăng nhập thành công

---

### `Views/Home/Index.cshtml`
**URL:** `GET /Home/Index` (mặc định `/`)  
**Tính năng:**
- **Khách (chưa đăng nhập):** Hiển thị landing page giới thiệu 4 tính năng chính (đặt phòng, mượn thiết bị, đăng ký đề tài, đăng ký App)
- **Đã đăng nhập:** Dashboard cá nhân hóa theo role
  - Banner chào mừng hiển thị họ tên + vai trò
  - Dải thông tin user (tên, email, vai trò)
  - Tile truy cập nhanh chung: Đặt phòng học, Mượn thiết bị, Đăng ký App
  - **Student thêm:** tile "Đăng ký đề tài"
  - **Lecturer thêm:** tile "Tạo đề tài", "Đề tài của tôi", "Chấm điểm"
  - **Admin thêm:** section "Quản trị hệ thống" gồm tile Đợt đăng ký, Quản lý thiết bị, Duyệt yêu cầu App

---

### `Views/Home/Privacy.cshtml`
**URL:** `GET /Home/Privacy`  
**Tính năng:** Trang chính sách bảo mật (static content).

---

### `Views/Shared/Error.cshtml`
**URL:** (tự động khi có lỗi)  
**Tính năng:** Hiển thị trang lỗi với Request ID.

---

## 2. Quản lý tài khoản (Admin)

### `Areas/Admin/Views/Account/Index.cshtml`
**URL:** `GET /Admin/Account/Index`  
**Role:** Admin  
**Tính năng:**
- Danh sách tất cả tài khoản (Admin, Giảng viên, Sinh viên) dạng bảng
- Tìm kiếm theo tên / email / MSSV (text input)
- Lọc theo vai trò (dropdown: Tất cả / Admin / Giảng viên / Sinh viên)
- Hiển thị: Họ tên, Email, Vai trò (badge màu), MSSV/Mã GV, Ngày tạo
- Nút **Sửa** (→ CreateEdit) và **Xóa** (có confirm dialog) từng tài khoản
- Nút **Thêm tài khoản** ở đầu trang
- Hiển thị tổng số kết quả lọc

---

### `Areas/Admin/Views/Account/CreateEdit.cshtml`
**URL:** `GET /Admin/Account/Create` | `GET /Admin/Account/Edit/{id}`  
**Role:** Admin  
**Tính năng:**
- Form dùng chung cho cả tạo mới và chỉnh sửa (detect qua `IsEditMode`)
- Trường: Họ tên, Email, Vai trò (dropdown), Mật khẩu (ẩn khi edit nếu không thay đổi), Số điện thoại, MSSV (chỉ hiện khi Role=Student), Mã GV (chỉ hiện khi Role=Lecturer)
- Validate client-side + server-side
- Breadcrumb điều hướng về danh sách

---

## 3. Quản lý đợt đăng ký (Admin)

### `Areas/Admin/Views/Registration/Periods.cshtml`
**URL:** `GET /Admin/Registration/Periods`  
**Role:** Admin  
**Tính năng:**
- Bảng danh sách tất cả đợt đăng ký đề tài
- Hiển thị: Tên đợt, Học phần, Thời gian bắt đầu–kết thúc, Trạng thái (Đang mở / Chưa mở / Đã kết thúc)
- Nút **Sửa** → EditPeriod
- Nút **Mở** (kích hoạt đợt, có confirm) — chỉ hiện khi chưa active
- Nút **Chốt** (đóng đợt + auto-reject PENDING, có confirm) — chỉ hiện khi đang active
- Nút **PDF** và **Excel** (export từ Google Sheet) — chỉ hiện khi có `GoogleSheetLink`
- Nút **Tạo đợt mới** ở đầu trang

---

### `Areas/Admin/Views/Registration/CreatePeriod.cshtml`
**URL:** `GET /Admin/Registration/CreatePeriod` | `GET /Admin/Registration/EditPeriod/{id}`  
**Role:** Admin  
**Tính năng:**
- Form tạo / chỉnh sửa đợt đăng ký
- Trường: Tên đợt, Tên học phần, Ngày bắt đầu, Ngày kết thúc, Link Google Sheet (tùy chọn)
- Validate ngày kết thúc phải sau ngày bắt đầu

---

## 4. Luồng Giảng viên (Lecturer Area)

### `Areas/Lecturer/Views/Registration/ActivePeriods.cshtml`
**URL:** `GET /Lecturer/Registration/ActivePeriods`  
**Role:** Lecturer  
**Tính năng:**
- Danh sách các đợt đăng ký đang hoạt động dạng card
- Mỗi card hiển thị: Tên đợt, Học phần, Hạn cuối
- Nút **Chấm điểm** → đến màn hình `Grading/List`
- Thông báo khi không có đợt nào đang diễn ra

---

### `Areas/Lecturer/Views/Registration/CreateTopic.cshtml`
**URL:** `GET /Lecturer/Registration/CreateTopic`  
**Role:** Lecturer  
**Tính năng:**
- Form tạo đề tài mới trong đợt đăng ký đang hoạt động
- Trường: Tên đề tài, Mô tả, Công nghệ (technologies), Số sinh viên tối đa (MaxStudents)
- Đề tài được tạo với Status=Open và gắn vào đợt đang active

---

### `Areas/Lecturer/Views/Registration/MyTopics.cshtml`
**URL:** `GET /Lecturer/Registration/MyTopics`  
**Role:** Lecturer  
**Tính năng:**
- Danh sách tất cả đề tài GV đã tạo trong đợt đang active
- Hiển thị: Tên đề tài, số SV đã đăng ký / tổng slot (badge màu: đỏ nếu đầy, xanh nếu còn chỗ)
- Nút **Xóa** đề tài (chỉ khi chưa có SV được duyệt)
- Nút **Tạo đề tài mới** khi có đợt active
- Thông báo khi không có đợt active

---

### `Areas/Lecturer/Views/Registration/Inbox.cshtml`
**URL:** `GET /Lecturer/Registration/Inbox`  
**Role:** Lecturer  
**Tính năng:**
- **Thống kê nhanh:** 3 ô stat (Chờ duyệt, Đã duyệt, Chỉ tiêu SV)
- **Danh sách đề xuất chờ duyệt:** mỗi card hiển thị tên đề tài, tên SV + MSSV, thời gian gửi, số lần sửa (nếu có), trích đoạn mô tả
- Nút **Xem & Duyệt** → Review
- **Bảng đã xử lý gần đây:** Sinh viên, Tên đề tài, Đợt, Trạng thái (Đã duyệt / Từ chối / Yêu cầu sửa), Ngày cập nhật

---

### `Areas/Lecturer/Views/Registration/Review.cshtml`
**URL:** `GET /Lecturer/Registration/Review/{id}`  
**Role:** Lecturer  
**Tính năng:**
- Xem chi tiết đề xuất của SV: tên đề tài, mô tả đầy đủ, tên SV, MSSV, đợt đăng ký, số lần sửa
- 3 action: **Duyệt** (approve) / **Yêu cầu sửa** (revise, bắt buộc nhập ghi chú) / **Từ chối** (reject)
- Trường nhập ghi chú/lý do (bắt buộc khi chọn "Yêu cầu sửa")

---

## 5. Luồng Sinh viên (Student Area)

### `Areas/Student/Views/Registration/ActivePeriods.cshtml`
**URL:** `GET /Student/Registration/ActivePeriods`  
**Role:** Student  
**Tính năng:**
- Danh sách các đợt đăng ký đang hoạt động dạng card
- Mỗi card: Tên đợt, Học phần, Hạn cuối
- Nút **Xem đề tài** → TopicList
- Thông báo khi không có đợt nào

---

### `Areas/Student/Views/Registration/NoPeriod.cshtml`
**URL:** Redirect tự động khi không có đợt active  
**Role:** Student  
**Tính năng:** Thông báo "Hiện không có đợt đăng ký nào đang diễn ra", link về trang chủ.

---

### `Areas/Student/Views/Registration/TopicList.cshtml`
**URL:** `GET /Student/Registration/TopicList` (kèm `periodId` query)  
**Role:** Student  
**Tính năng:**
- Breadcrumb: Đợt đăng ký → Tên đợt hiện tại
- Header: Tên đợt, học phần, hạn cuối
- Nút **Đề xuất đề tài mới** → ProposeNew
- **Bộ lọc:** tìm tên đề tài / mô tả (text), lọc theo giảng viên (dropdown)
- Hiển thị đề tài dạng card (3 cột):
  - Tên đề tài, badge trạng thái (Đã đăng ký / Hết chỗ)
  - Mô tả (truncate 3 dòng), tags công nghệ
  - Tên giảng viên, số SV đã đăng ký / tổng slot
  - Thanh progress slot (xanh → đỏ khi đầy)
  - Nút **Đăng ký** (confirm dialog) / **Đã đăng ký** / **Hết chỗ**
- Hiển thị tổng số đề tài
- Link **Quay lại danh sách đợt** và **Xem đăng ký của tôi**

---

### `Areas/Student/Views/Registration/ProposeNew.cshtml`
**URL:** `GET /Student/Registration/ProposeNew`  
**Role:** Student  
**Tính năng:**
- Form đề xuất đề tài mới
- Trường: Tên đề tài đề xuất, Mô tả, Giảng viên hướng dẫn (dropdown), Công nghệ dự kiến
- Submit → tạo Topic(Status=Proposed) + Registration(Status=PENDING) + notify GV

---

### `Areas/Student/Views/Registration/ReviseProposal.cshtml`
**URL:** `GET /Student/Registration/ReviseProposal/{id}`  
**Role:** Student (chỉ khi Status=REVISION_REQUIRED)  
**Tính năng:**
- Form chỉnh sửa đề xuất sau khi GV yêu cầu sửa
- Hiển thị ghi chú của GV (note) nổi bật ở đầu
- Điền sẵn thông tin cũ (tên đề tài, mô tả, công nghệ)
- Submit → cập nhật Topic + tăng RevisionCount + trả về PENDING

---

### `Areas/Student/Views/Registration/MyRegistrations.cshtml`
**URL:** `GET /Student/Registration/MyRegistrations`  
**Role:** Student  
**Tính năng:**
- Lịch sử tất cả lượt đăng ký đề tài của SV (card list)
- Mỗi card: Tên đề tài, Giảng viên, Đợt, Ngày đăng ký, Badge trạng thái (APPROVED/PENDING/REJECTED/REVISION_REQUIRED)
- Nút **Chỉnh sửa đề xuất** (chỉ khi REVISION_REQUIRED)
- Nút **Hủy đăng ký** với confirm (chỉ khi PENDING hoặc REVISION_REQUIRED)
- Box hiển thị ghi chú từ GV (màu vàng cảnh báo khi cần sửa, xanh info khi đã xử lý)
- Nút **Đăng ký thêm** khi còn đợt active

---

## 6. Đặt phòng học

### `Views/Home/RoomList.cshtml`
**URL:** `GET /Home/RoomList`  
**Tính năng:**
- Danh sách tất cả phòng học khả dụng dạng card/bảng
- Nút **Xem lịch** → WeeklySchedule của phòng tương ứng

---

### `Views/Room/WeeklySchedule.cshtml`
**URL:** `GET /Room/WeeklySchedule?roomId={id}`  
**Tính năng (AllowAnonymous — ai cũng xem được):**
- Lịch theo tuần dạng FullCalendar (time-grid view)
- Hiển thị các booking đã có (màu sắc theo trạng thái)
- Điều hướng qua lại theo tuần
- Click vào ô giờ trống → mở modal xác nhận đặt phòng (chỉ khi đã đăng nhập)
- Click vào sự kiện → modal hiển thị thông tin chi tiết booking
- Ngày trong quá khứ và Chủ nhật bị disable

---

### `Views/Shared/CreateBooking.cshtml`
**URL:** Modal / `GET /Room/CreateBooking`  
**Tính năng:**
- Form đặt phòng: Phòng, Ngày, Giờ bắt đầu, Giờ kết thúc, Mục đích sử dụng
- Validate không trùng lịch

---

### `Views/Room/PendingBookings.cshtml`
**URL:** `GET /Room/PendingBookings` (Admin/Lecturer)  
**Tính năng:**
- Danh sách các yêu cầu đặt phòng chờ duyệt
- Nút **Duyệt** / **Từ chối** từng yêu cầu

---

## 7. Mượn thiết bị

### `Views/Device/Catalog.cshtml`
**URL:** `GET /Device/Catalog`  
**Tính năng (mọi người dùng đã đăng nhập):**
- Danh sách thiết bị có thể mượn dạng bảng
- Thanh lọc: tìm tên (real-time JS), lọc theo danh mục (pill buttons)
- Hiển thị: Tên thiết bị, Danh mục, Tình trạng (Còn / Đã cho mượn / Hỏng)
- Nút **Mượn** → BorrowForm (chỉ khi thiết bị Available)

---

### `Views/Device/BorrowForm.cshtml`
**URL:** `GET /Device/BorrowForm?deviceId={id}`  
**Tính năng:**
- Form gửi yêu cầu mượn thiết bị
- Trường: Thiết bị (pre-filled nếu có deviceId), Ngày mượn, Ngày trả dự kiến, Mục đích
- Submit → tạo DeviceRequest(Status=Pending)

---

### `Views/Device/BorrowSuccess.cshtml`
**URL:** `GET /Device/BorrowSuccess?requestId={id}`  
**Tính năng:** Trang xác nhận gửi yêu cầu mượn thành công, hiển thị thông tin tóm tắt.

---

### `Views/Device/Index.cshtml`
**URL:** `GET /Device/Index`  
**Role:** Admin  
**Tính năng:**
- Bảng quản lý toàn bộ thiết bị kho
- Lọc theo: trạng thái (filterStatus), danh mục (filterCategory), tình trạng (filterCondition)
- Thao tác: **Sửa** (→ Edit), **Xóa**, **Đổi tình trạng** (Bình thường / Hỏng), **Đánh dấu Sẵn sàng**
- Nút **Thêm thiết bị** → Create

---

### `Views/Device/Create.cshtml`
**URL:** `GET /Device/Create`  
**Role:** Admin  
**Tính năng:** Form thêm thiết bị mới (Tên, Danh mục, Mô tả, Tình trạng ban đầu).

---

### `Views/Device/Edit.cshtml`
**URL:** `GET /Device/Edit/{id}`  
**Role:** Admin  
**Tính năng:** Form chỉnh sửa thông tin thiết bị (điền sẵn dữ liệu cũ).

---

### `Views/Device/BorrowRequests.cshtml`
**URL:** `GET /Device/BorrowRequests`  
**Role:** Admin  
**Tính năng:**
- Danh sách tất cả yêu cầu mượn thiết bị
- Hiển thị: Thiết bị, Người mượn, Ngày mượn, Ngày trả, Mục đích, Trạng thái
- Nút **Duyệt** (Approve) / **Từ chối** (kèm lý do) / **Đã trả** (MarkReturned) theo trạng thái hiện tại

---

## 8. Đăng ký phần mềm (App Registration)

### `Views/AppRegistration/Create.cshtml`
**URL:** `GET /AppRegistration/Create`  
**Tính năng (mọi người dùng đã đăng nhập):**
- Form gửi yêu cầu đăng ký kiểm duyệt ứng dụng
- Trường: Tên ứng dụng, Mô tả, Link APK, Link Demo, Ghi chú thêm
- Submit → tạo AppRegistrationRequest

---

### `Views/AppRegistration/PendingRequests.cshtml`
**URL:** `GET /AppRegistration/PendingRequests`  
**Role:** Admin, Lecturer  
**Tính năng:**
- Danh sách các yêu cầu đăng ký App đang chờ xử lý
- Hiển thị: Tên app, Người gửi, Ngày gửi, Link APK/Demo, Trạng thái
- **Admin:** Nút **Phân công GV** (assign lecturer) + **Duyệt**
- **Lecturer:** Nút **Duyệt** / **Từ chối** các yêu cầu được phân công

---

## 9. Chấm điểm (Grading)

### `Views/Grading/List.cshtml`
**URL:** `GET /Grading/List?periodId={id}`  
**Role:** Lecturer, Admin  
**Tính năng:**
- Breadcrumb: Đợt đăng ký → Chấm điểm — Tên đợt
- Thống kê: Đã chấm / Chưa chấm / Chờ sync
- Thanh tìm kiếm MSSV / tên SV
- Bảng danh sách SV từ Google Sheet (đồng bộ từ BangDiem):
  - MSSV, Họ tên, Tên đề tài, Giảng viên HD
  - Điểm (badge màu: xanh lá ≥8, xanh dương 5–8, đỏ <5, hoặc "—" nếu chưa chấm)
  - Badge trạng thái sync (Synced / Pending / Failed)
  - Nút **Chấm điểm** → Grade
- Dòng chưa chấm được highlight nền vàng nhạt

---

### `Views/Grading/Grade.cshtml`
**URL:** `GET /Grading/Grade?periodId={id}&sheetId={sid}&mssv={mssv}`  
**Role:** Lecturer, Admin  
**Tính năng:**
- Hiển thị thông tin SV: MSSV, Họ tên, Tên đề tài
- Input nhập điểm (0–10, bước 0.1)
- Nút **Lưu điểm** → ghi vào `GradeRecord` (SyncStatus=Pending), background worker sẽ sync lên Sheet

---

## 10. Các view phụ trợ (Shared / Dev)

### `Views/Shared/_Layout.cshtml`
Layout chính cho toàn bộ app: navbar, sidebar (nếu có), flash alerts, footer.

### `Views/Shared/_Alerts.cshtml`
Partial hiển thị TempData `"Success"` / `"Error"` / `"Info"` dạng alert box.

### `Views/Shared/_SuccessToast.cshtml`
Partial toast notification dạng popup nhỏ góc màn hình.

### `Views/Topic/List.cshtml`
**URL:** `GET /Topic/List?periodId={id}`  
**Tính năng:** Xem danh sách đề tài đồng bộ từ Google Sheet của đợt, kèm nút đăng ký (dùng cho flow Sheet-based, khác với flow DB-based ở Student Area).

### `Views/SeedAdmin/Index.cshtml`
**URL:** `GET /SeedAdmin/Index` (dev only)  
**Tính năng:** Tạo nhanh tài khoản test với role tùy chọn, seed dữ liệu sinh viên.

---

## Tóm tắt số lượng

| Nhóm | Số view |
|---|---|
| Chung / Auth | 4 |
| Admin — Tài khoản | 2 |
| Admin — Đợt đăng ký | 2 |
| Lecturer | 5 |
| Student | 6 |
| Đặt phòng | 3 |
| Thiết bị | 6 |
| App Registration | 2 |
| Chấm điểm | 2 |
| Phụ trợ / Dev | 4 |
| **Tổng** | **36** |
