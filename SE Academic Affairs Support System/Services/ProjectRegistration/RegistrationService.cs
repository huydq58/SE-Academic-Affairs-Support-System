using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Helper;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.Services.Email;
using SE_Academic_Affairs_Support_System.Services.EmailNotification;
using SE_Academic_Affairs_Support_System.Services.Excel;
using SE_Academic_Affairs_Support_System.Services.NotificationSevices;
using SE_Academic_Affairs_Support_System.ViewModels;

namespace SE_Academic_Affairs_Support_System.Services.ProjectRegistration
{
    public class RegistrationService : IRegistrationService
    {
        private readonly AppDbContext _db;
        private readonly INotificationService _notif;
        private readonly GoogleSheetsService _sheets;
        private readonly IEmailService _email;
        private readonly IEmailNotificationService _emailNotif;
        private readonly ILogger<RegistrationService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IExcelService _excel;

        public RegistrationService(
            AppDbContext db,
            INotificationService notif,
            GoogleSheetsService sheets,
            IEmailService email,
            IEmailNotificationService emailNotif,
            ILogger<RegistrationService> logger,
            IServiceScopeFactory scopeFactory,
            IExcelService excel)
        {
            _db = db;
            _notif = notif;
            _sheets = sheets;
            _email = email;
            _emailNotif = emailNotif;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _excel = excel;
        }

        // ── Periods ───────────────────────────────────────────────────────────
        public async Task<RegistrationPeriod?> GetActivePeriodAsync()
            => await _db.RegistrationPeriods
                .FirstOrDefaultAsync(p => p.IsActive && p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow);

        public async Task<List<RegistrationPeriod>> GetActivePeriodsAsync()
            => await _db.RegistrationPeriods
                .Where(p => p.IsActive && p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow)
                .OrderByDescending(p => p.EndDate)
                .ToListAsync();

        // Trả về đợt đang mở mà sinh viên được phép tham gia
        public async Task<RegistrationPeriod?> GetActivePeriodForStudentAsync(int studentProfileId)
            => await _db.RegistrationPeriods
                .FirstOrDefaultAsync(p =>
                    p.IsActive &&
                    p.StartDate <= DateTime.UtcNow &&
                    p.EndDate >= DateTime.UtcNow &&
                    (!p.RestrictToAllowedStudents ||
                     p.AllowedStudents.Any(s => s.StudentProfileId == studentProfileId)));

        public async Task<List<RegistrationPeriod>> GetAllPeriodsAsync()
            => await _db.RegistrationPeriods.OrderByDescending(p => p.StartDate).ToListAsync();

        public async Task CreatePeriodAsync(PeriodFormViewModel vm)
        {
            var period = new RegistrationPeriod
            {
                Name = vm.Name,
                CourseName = vm.CourseName,
                GoogleSheetLink = vm.GoogleSheetLink,
                StartDate = vm.StartDate,
                EndDate = vm.EndDate,
                IsActive = vm.IsActive,
                RestrictToAllowedStudents = vm.RestrictToAllowedStudents,
                ReportDeadline = vm.ReportDeadline
            };
            _db.RegistrationPeriods.Add(period);
            await _db.SaveChangesAsync();

            // Có hạn nộp ngay khi tạo → thông báo SV đã được duyệt (nếu có)
            if (period.ReportDeadline.HasValue)
                QueueReportDeadlineAnnouncement(period.Id);
        }

        public async Task UpdatePeriodAsync(PeriodFormViewModel vm)
        {
            var period = await _db.RegistrationPeriods.FindAsync(vm.Id)
                         ?? throw new InvalidOperationException("Không tìm thấy đợt đăng ký");

            var oldDeadline = period.ReportDeadline;

            period.Name = vm.Name;
            period.CourseName = vm.CourseName;
            period.StartDate = vm.StartDate;
            period.EndDate = vm.EndDate;
            period.IsActive = vm.IsActive;
            period.RestrictToAllowedStudents = vm.RestrictToAllowedStudents;
            period.ReportDeadline = vm.ReportDeadline;

            // Đổi hạn nộp → reset trạng thái nhắc nhở để chu kỳ nhắc tính lại
            if (vm.ReportDeadline != oldDeadline)
                period.LastReminderSentDate = null;

            await _db.SaveChangesAsync();

            // Đặt mới / thay đổi hạn nộp → thông báo SV
            if (vm.ReportDeadline.HasValue && vm.ReportDeadline != oldDeadline)
                QueueReportDeadlineAnnouncement(period.Id);
        }

        // Gửi nền (fire-and-forget) thông báo hạn nộp cho SV đã APPROVED — scope riêng, không chặn request.
        public void QueueReportDeadlineAnnouncement(int periodId)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var emailNotif = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();

                    var period = await db.RegistrationPeriods.FindAsync(periodId);
                    if (period?.ReportDeadline == null) return;

                    var students = await db.Registrations
                        .Where(r => r.RegistrationPeriodId == periodId && r.Status == RegistrationStatus.APPROVED)
                        .Select(r => new { r.Student.User.Email, r.Student.User.FullName, r.Student.StudentCode })
                        .Distinct()
                        .ToListAsync();

                    foreach (var s in students)
                        if (!string.IsNullOrWhiteSpace(s.Email))
                            await emailNotif.NotifyReportDeadlineAsync(
                                s.Email!, s.FullName ?? s.StudentCode, period.Name,
                                period.ReportDeadline.Value, isReminder: false, daysLeft: null);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Gửi thông báo hạn nộp báo cáo thất bại cho period {PeriodId}", periodId);
                }
            });
        }

        public async Task SetPeriodActiveAsync(int periodId)
        {
            var period = await _db.RegistrationPeriods.FindAsync(periodId)
                         ?? throw new InvalidOperationException("Không tìm thấy đợt đăng ký");

            period.IsActive = true;

            await _db.SaveChangesAsync();
        }

        public async Task ClosePeriodAndAutoRejectPendingAsync(int periodId)
        {
            var period = await _db.RegistrationPeriods.FindAsync(periodId);
            var periodName = period?.Name ?? string.Empty;

            var pending = await _db.Registrations
                .Where(r => r.RegistrationPeriodId == periodId && r.Status == RegistrationStatus.PENDING)
                .ToListAsync();

            foreach (var reg in pending)
            {
                reg.Status = RegistrationStatus.REJECTED;
                reg.LecturerNote = "Đợt đăng ký đã kết thúc, đề xuất chưa được duyệt bị hủy tự động.";
                reg.UpdatedAt = DateTime.UtcNow;

                var studentUser = await _db.StudentProfiles
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.Id == reg.StudentProfileId);

                if (studentUser != null)
                {
                    await _notif.SendAsync(studentUser.UserId,
                        "Đề xuất đề tài của bạn đã bị hủy do đợt đăng ký kết thúc.",
                        $"/Student/Registration/MyRegistrations");

                    if (studentUser.User?.Email != null)
                        await _emailNotif.NotifyTopicAutoRejectedAsync(
                            studentUser.User.Email,
                            studentUser.User.FullName ?? studentUser.StudentCode,
                            reg.ProposedTitle ?? "đề tài của bạn",
                            periodName);
                }
            }

            if (period != null) period.IsActive = false;

            await _db.SaveChangesAsync();
        }

        // ── Lecturer Topics ───────────────────────────────────────────────────
        public async Task<List<TopicManageRow>> GetLecturerTopicsAsync(int lecturerProfileId, int? periodId = null)
        {
            var topics = await _db.Topics
                .Where(t => t.LecturerProfileId == lecturerProfileId)
                .Where(t => !periodId.HasValue || t.RegistrationPeriodId == periodId.Value)
                .Include(t => t.RegistrationPeriod)
                .Include(t => t.Registrations)
                    .ThenInclude(r => r.Student)
                        .ThenInclude(s => s.User)
                .ToListAsync();

            return topics.Select(t =>
            {
                var approved = t.Registrations
                    .Where(r => r.Status == RegistrationStatus.APPROVED)
                    .OrderBy(r => r.Id)
                    .ToList();
                return new TopicManageRow
                {
                    TopicId = t.Id,
                    PeriodId = t.RegistrationPeriodId,
                    Title = t.Title,
                    MaxStudents = t.MaxStudents,
                    RegisteredCount = approved.Count,
                    Status = t.Status,
                    PeriodName = t.RegistrationPeriod.Name,
                    StudentName1 = approved.ElementAtOrDefault(0)?.Student?.User?.FullName,
                    StudentName2 = approved.ElementAtOrDefault(1)?.Student?.User?.FullName
                };
            }).ToList();
        }

        public async Task CreateTopicAsync(CreateTopicViewModel vm, int lecturerProfileId)
        {
            var lecturer = await _db.LecturerProfiles
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == lecturerProfileId);

            var topic = new Topic
            {
                Title = vm.Title,
                Description = vm.Description,
                Requirements = vm.Requirements,
                Technologies = vm.Technologies,
                MaxStudents = vm.MaxStudents,
                Status = TopicStatus.Open,
                LecturerProfileId = lecturerProfileId,
                RegistrationPeriodId = vm.RegistrationPeriodId,
                Note = vm.Note
            };
            _db.Topics.Add(topic);
            await _db.SaveChangesAsync();

            // Outbox: queue topic creation to Google Sheets
            _db.TopicSyncRecords.Add(new TopicSyncRecord
            {
                TopicId = topic.Id,
                PeriodId = vm.RegistrationPeriodId,
                TopicTitle = vm.Title,
                TopicDescription = vm.Description,
                Technologies = vm.Technologies,
                Requirements = vm.Requirements,
                MaxStudents = vm.MaxStudents,
                LecturerName = lecturer?.User?.FullName ?? string.Empty,
                LecturerCode = lecturer?.LecturerCode ?? string.Empty,
                Note = vm.Note
            });
            await _db.SaveChangesAsync();
        }

        public async Task DeleteTopicAsync(int topicId, int lecturerProfileId)
        {
            var topic = await _db.Topics.FindAsync(topicId);
            if (topic == null || topic.LecturerProfileId != lecturerProfileId) return;

            bool hasApproved = await _db.Registrations
                .AnyAsync(r => r.TopicId == topicId && r.Status == RegistrationStatus.APPROVED);
            if (hasApproved) throw new InvalidOperationException("Không thể xóa đề tài đã có SV được duyệt.");

            // Xóa các bản ghi liên quan trước (cascade delete bị tắt)
            var syncRecords = await _db.TopicSyncRecords.Where(r => r.TopicId == topicId).ToListAsync();
            _db.TopicSyncRecords.RemoveRange(syncRecords);

            var pendingRegs = await _db.Registrations
                .Where(r => r.TopicId == topicId && r.Status != RegistrationStatus.APPROVED)
                .ToListAsync();
            _db.Registrations.RemoveRange(pendingRegs);

            _db.Topics.Remove(topic);
            await _db.SaveChangesAsync();
        }

        // ── Browse Topics (Student) ───────────────────────────────────────────
        public async Task<TopicListViewModel?> GetTopicListForStudentAsync(
            int studentProfileId, string? keyword, int? lecturerId)
        {
            var period = await GetActivePeriodForStudentAsync(studentProfileId);
            if (period == null) return null;

            // IDs của đề tài SV đã đăng ký trong đợt (để đánh dấu "Đã đăng ký")
            var registeredTopicIds = await _db.Registrations
                .Where(r => r.StudentProfileId == studentProfileId
                         && r.Status != RegistrationStatus.REJECTED
                         && r.RegistrationPeriodId == period.Id)
                .Select(r => r.TopicId)
                .ToListAsync();

            var query = _db.Topics
                .Where(t => t.RegistrationPeriodId == period.Id && t.Status == TopicStatus.Open)
                .Include(t => t.Lecturer).ThenInclude(l => l.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(t =>
                    t.Title.Contains(keyword) || t.Description.Contains(keyword));

            if (lecturerId.HasValue)
                query = query.Where(t => t.LecturerProfileId == lecturerId.Value);

            var dbCards = await query
                .Select(t => new TopicCardViewModel
                {
                    TopicId = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Requirements = t.Requirements,
                    Technologies = t.Technologies,
                    LecturerName = t.Lecturer.User.FullName,
                    LecturerProfileId = t.LecturerProfileId,
                    MaxStudents = t.MaxStudents,
                    RegisteredCount = t.Registrations
                        .Count(r => r.Status == RegistrationStatus.APPROVED),
                    AlreadyRegistered = registeredTopicIds.Contains(t.Id)
                })
                .ToListAsync();

            var lecturers = await _db.LecturerProfiles
                .Include(l => l.User)
                .Select(l => new LecturerSelectItem
                {
                    Id = l.Id,
                    FullName = l.User.FullName,
                    LecturerCode = l.LecturerCode
                }).ToListAsync();

            return new TopicListViewModel
            {
                Period = period,
                Topics = dbCards,
                SearchKeyword = keyword,
                FilterLecturerId = lecturerId,
                Lecturers = lecturers
            };
        }

        // ── Flow A: Register Existing Topic ──────────────────────────────────
        public async Task<(bool Success, string Message)> RegisterExistingTopicAsync(
            int studentProfileId, int topicId)
        {
            var period = await GetActivePeriodForStudentAsync(studentProfileId);
            if (period == null)
                return (false, "Hiện tại không có đợt đăng ký nào đang mở hoặc bạn không có trong danh sách được phép đăng ký.");

            // ReadCommitted + UPDLOCK trên đúng row đề tài:
            // - Hai transaction đồng thời không thể cùng giữ UPDLOCK trên cùng một row
            //   → ngăn race condition đếm slot mà không cần range lock toàn bảng (Serializable).
            // - Các row khác trong Topics và Registrations không bị ảnh hưởng.
            using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                var topic = await _db.Topics
                    .FromSqlRaw("SELECT * FROM Topics WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", topicId)
                    .FirstOrDefaultAsync();

                if (topic == null || topic.Status != TopicStatus.Open)
                    return (false, "Đề tài không tồn tại hoặc đã đóng đăng ký.");

                // COUNT trong SQL — không load toàn bộ Registrations vào memory
                int approvedCount = await _db.Registrations
                    .CountAsync(r => r.TopicId == topicId && r.Status == RegistrationStatus.APPROVED);

                if (approvedCount >= topic.MaxStudents)
                    return (false, "Đề tài đã hết chỗ.");

                bool alreadyRegistered = await _db.Registrations
                    .AnyAsync(r => r.StudentProfileId == studentProfileId
                                && r.TopicId == topicId
                                && r.Status != RegistrationStatus.REJECTED);
                if (alreadyRegistered)
                    return (false, "Bạn đã đăng ký đề tài này rồi.");

                bool hasApproved = await _db.Registrations
                    .AnyAsync(r => r.StudentProfileId == studentProfileId
                                && r.RegistrationPeriodId == period.Id
                                && r.Status == RegistrationStatus.APPROVED);
                if (hasApproved)
                    return (false, "Bạn đã có đề tài được duyệt trong đợt này.");

                _db.Registrations.Add(new Registration
                {
                    StudentProfileId = studentProfileId,
                    TopicId = topicId,
                    LecturerProfileId = topic.LecturerProfileId,
                    RegistrationPeriodId = period.Id,
                    Status = RegistrationStatus.APPROVED,
                    UpdatedAt = DateTime.UtcNow
                });

                if (approvedCount + 1 >= topic.MaxStudents)
                    topic.Status = TopicStatus.Closed;

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                // Capture primitives for background task (không capture DbContext của request)
                var capturedStudentId = studentProfileId;
                var capturedLecturerProfileId = topic.LecturerProfileId;
                var capturedSheetRowIndex = topic.SheetRowIndex;
                var capturedTopicTitle = topic.Title;
                var capturedSheetLink = period.GoogleSheetLink;
                var capturedPeriodName = period.Name;

                // Fire-and-forget: sheet sync + email chạy song song với response.
                // IServiceScopeFactory tạo scope độc lập với DbContext riêng,
                // tránh ObjectDisposedException khi scope của request kết thúc.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var emailNotif = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();

                        var student = await db.StudentProfiles
                            .AsNoTracking()
                            .Where(s => s.Id == capturedStudentId)
                            .Select(s => new { s.StudentCode, s.User.Email, DisplayName = s.User.FullName ?? s.StudentCode })
                            .FirstOrDefaultAsync();

                        if (capturedSheetRowIndex.HasValue && student != null)
                        {
                            var sheetId = GoogleSheetHelper.ExtractSheetId(capturedSheetLink);
                            if (sheetId != null)
                            {
                                try
                                {
                                    await _sheets.RegisterAsync(new RegisterTopicRequest
                                    {
                                        SheetId = sheetId,
                                        RowIndex = capturedSheetRowIndex.Value,
                                        StudentId = student.StudentCode,
                                        StudentName = student.DisplayName
                                    });
                                }
                                catch { }
                            }
                        }

                        if (student?.Email != null)
                        {
                            var lecturerName = await db.LecturerProfiles
                                .AsNoTracking()
                                .Where(l => l.Id == capturedLecturerProfileId)
                                .Select(l => l.User.FullName ?? l.LecturerCode)
                                .FirstOrDefaultAsync() ?? string.Empty;

                            try
                            {
                                await emailNotif.NotifyTopicRegisteredAsync(
                                    student.Email,
                                    student.DisplayName,
                                    capturedTopicTitle,
                                    lecturerName,
                                    capturedPeriodName);
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Post-commit notification failed for studentId={StudentId} topicId={TopicId}",
                            capturedStudentId, topicId);
                    }
                });

                return (true, "Đăng ký thành công!");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        // ── Flow B: Submit Proposal ───────────────────────────────────────────
        public async Task<(bool Success, string Message)> SubmitProposalAsync(
            int studentProfileId, ProposalViewModel vm)
        {
            var period = await GetActivePeriodForStudentAsync(studentProfileId);
            if (period == null)
                return (false, "Hiện tại không có đợt đăng ký nào đang mở hoặc bạn không có trong danh sách được phép đăng ký.");

            bool hasApproved = await _db.Registrations
                .AnyAsync(r => r.StudentProfileId == studentProfileId
                            && r.RegistrationPeriodId == period.Id
                            && r.Status == RegistrationStatus.APPROVED);
            if (hasApproved)
                return (false, "Bạn đã có đề tài được duyệt trong đợt này.");

            bool hasPending = await _db.Registrations
                .AnyAsync(r => r.StudentProfileId == studentProfileId
                            && r.RegistrationPeriodId == period.Id
                            && (r.Status == RegistrationStatus.PENDING
                             || r.Status == RegistrationStatus.REVISION_REQUIRED));
            if (hasPending)
                return (false, "Bạn đang có đề xuất chờ duyệt. Vui lòng chờ kết quả hoặc hủy đề xuất cũ.");

            // Create a pseudo-topic for tracking
            var topic = new Topic
            {
                Title = vm.Title,
                Description = vm.Description,
                Technologies = vm.Technologies,
                MaxStudents = 1,
                Status = TopicStatus.Proposed,
                LecturerProfileId = vm.LecturerProfileId,
                ProposedByStudentId = studentProfileId,
                RegistrationPeriodId = period.Id
            };
            _db.Topics.Add(topic);
            await _db.SaveChangesAsync(); // get topic.Id

            var registration = new Registration
            {
                StudentProfileId = studentProfileId,
                TopicId = topic.Id,
                LecturerProfileId = vm.LecturerProfileId,
                RegistrationPeriodId = period.Id,
                Status = RegistrationStatus.PENDING,
                ProposedTitle = vm.Title,
                ProposedDescription = vm.Description,
                ProposedTechnologies = vm.Technologies,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Registrations.Add(registration);
            await _db.SaveChangesAsync();

            // Notify lecturer (in-app)
            var lecturer = await _db.LecturerProfiles.Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == vm.LecturerProfileId);
            if (lecturer != null)
                await _notif.SendAsync(lecturer.UserId,
                    $"Sinh viên vừa gửi đề xuất đề tài mới chờ bạn duyệt.",
                    $"/Lecturer/Registration/Review/{registration.Id}");

            // Email lecturer
            var student = await _db.StudentProfiles.Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == studentProfileId);
            if (lecturer?.User?.Email != null && student != null)
            {
                try
                {
                    await _email.SendTopicProposalToLecturerAsync(
                        toEmail: lecturer.User.Email,
                        lecturerName: lecturer.User.FullName ?? lecturer.LecturerCode,
                        studentName: student.User.FullName ?? student.StudentCode,
                        studentCode: student.StudentCode,
                        topicTitle: vm.Title,
                        description: vm.Description,
                        reviewUrl: $"/Lecturer/Registration/Review/{registration.Id}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể gửi email thông báo cho GV {LecturerId}", lecturer.Id);
                }
            }
            else
            {
                _logger.LogWarning("Giảng viên {LecturerId} không có email — bỏ qua gửi email thông báo đề xuất.", vm.LecturerProfileId);
            }

            return (true, "Đề xuất của bạn đã được gửi thành công. Vui lòng chờ giảng viên xem xét.");
        }

        public async Task<(bool Success, string Message)> ResubmitProposalAsync(
            int studentProfileId, int registrationId, ProposalViewModel vm)
        {
            var reg = await _db.Registrations
                .Include(r => r.Topic)
                .FirstOrDefaultAsync(r => r.Id == registrationId && r.StudentProfileId == studentProfileId);

            if (reg == null || reg.Status != RegistrationStatus.REVISION_REQUIRED)
                return (false, "Không tìm thấy đề xuất hợp lệ để chỉnh sửa.");

            // Update topic
            reg.Topic.Title = vm.Title;
            reg.Topic.Description = vm.Description;
            reg.Topic.Technologies = vm.Technologies;

            // Update registration
            reg.ProposedTitle = vm.Title;
            reg.ProposedDescription = vm.Description;
            reg.ProposedTechnologies = vm.Technologies;
            reg.LecturerProfileId = vm.LecturerProfileId;
            reg.Status = RegistrationStatus.PENDING;
            reg.LecturerNote = null;
            reg.RevisionCount += 1;
            reg.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Notify lecturer (in-app + email)
            var lecturer = await _db.LecturerProfiles.Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == vm.LecturerProfileId);
            if (lecturer != null)
                await _notif.SendAsync(lecturer.UserId,
                    $"Sinh viên đã gửi lại đề xuất sau khi chỉnh sửa (lần {reg.RevisionCount}).",
                    $"/Lecturer/Registration/Review/{reg.Id}");

            if (lecturer?.User?.Email != null)
            {
                var student2 = await _db.StudentProfiles.Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.Id == studentProfileId);
                if (student2 != null)
                {
                    try
                    {
                        await _email.SendTopicProposalToLecturerAsync(
                            toEmail: lecturer.User.Email,
                            lecturerName: lecturer.User.FullName ?? lecturer.LecturerCode,
                            studentName: student2.User.FullName ?? student2.StudentCode,
                            studentCode: student2.StudentCode,
                            topicTitle: vm.Title,
                            description: vm.Description,
                            reviewUrl: $"/Lecturer/Registration/Review/{reg.Id}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Không thể gửi email thông báo chỉnh sửa cho GV {LecturerId}", lecturer.Id);
                    }
                }
            }

            return (true, "Đề xuất đã được cập nhật và gửi lại thành công.");
        }

        public async Task<(bool Success, string Message)> CancelRegistrationAsync(
            int studentProfileId, int registrationId)
        {
            var reg = await _db.Registrations
                .FirstOrDefaultAsync(r => r.Id == registrationId && r.StudentProfileId == studentProfileId);

            if (reg == null)
                return (false, "Không tìm thấy đăng ký.");

            if (reg.Status == RegistrationStatus.APPROVED || reg.Status == RegistrationStatus.REJECTED)
                return (false, "Không thể hủy đăng ký ở trạng thái này.");

            reg.Status = RegistrationStatus.REJECTED;
            reg.UpdatedAt = DateTime.UtcNow;
            reg.LecturerNote = "Sinh viên tự hủy đăng ký.";
            await _db.SaveChangesAsync();

            return (true, "Đã hủy đăng ký thành công.");
        }

        // ── My Registrations ──────────────────────────────────────────────────
        public async Task<MyRegistrationsViewModel> GetMyRegistrationsAsync(int studentProfileId)
        {
            var period = await GetActivePeriodAsync();

            var regs = await _db.Registrations
                .Where(r => r.StudentProfileId == studentProfileId)
                .Include(r => r.Topic)
                .Include(r => r.Lecturer).ThenInclude(l => l.User)
                .Include(r => r.RegistrationPeriod)
                .OrderByDescending(r => r.UpdatedAt)
                .ToListAsync();

            return new MyRegistrationsViewModel
            {
                HasActivePeriod = period != null,
                ActivePeriodId = period?.Id,
                Registrations = regs.Select(r => new RegistrationRowViewModel
                {
                    RegistrationId = r.Id,
                    RegistrationPeriodId = r.RegistrationPeriodId,
                    TopicTitle = r.ProposedTitle ?? r.Topic.Title,
                    LecturerName = r.Lecturer.User.FullName,
                    PeriodName = r.RegistrationPeriod.Name,
                    Status = r.Status,
                    LecturerNote = r.LecturerNote,
                    CreatedAt = r.CreatedAt
                }).ToList()
            };
        }

        public async Task<ProposalViewModel?> GetProposalForRevisionAsync(
            int registrationId, int studentProfileId)
        {
            var reg = await _db.Registrations
                .Include(r => r.RegistrationPeriod)
                .FirstOrDefaultAsync(r => r.Id == registrationId
                    && r.StudentProfileId == studentProfileId
                    && r.Status == RegistrationStatus.REVISION_REQUIRED);

            if (reg == null) return null;

            var lecturers = await _db.LecturerProfiles.Include(l => l.User)
                .Select(l => new LecturerSelectItem
                { Id = l.Id, FullName = l.User.FullName, LecturerCode = l.LecturerCode })
                .ToListAsync();

            return new ProposalViewModel
            {
                Title = reg.ProposedTitle ?? string.Empty,
                Description = reg.ProposedDescription ?? string.Empty,
                Technologies = reg.ProposedTechnologies,
                LecturerProfileId = reg.LecturerProfileId,
                RegistrationPeriodId = reg.RegistrationPeriodId,
                ExistingRegistrationId = reg.Id,
                PreviousLecturerNote = reg.LecturerNote,
                AvailableLecturers = lecturers
            };
        }

        // ── Lecturer Inbox ────────────────────────────────────────────────────
        public async Task<LecturerInboxViewModel> GetLecturerInboxAsync(int lecturerProfileId)
        {
            var lecturer = await _db.LecturerProfiles.FindAsync(lecturerProfileId);

            var allRegs = await _db.Registrations
                .Where(r => r.LecturerProfileId == lecturerProfileId)
                .Include(r => r.Student).ThenInclude(s => s.User)
                .Include(r => r.RegistrationPeriod)
                .OrderByDescending(r => r.UpdatedAt)
                .ToListAsync();

            static ProposalReviewItem Map(Registration r) => new()
            {
                RegistrationId = r.Id,
                StudentName = r.Student.User.FullName,
                StudentCode = r.Student.StudentCode,
                ProposedTitle = r.ProposedTitle ?? r.Topic?.Title ?? "(Không tiêu đề)",
                ProposedDescription = r.ProposedDescription ?? r.Topic?.Description ?? string.Empty,
                ProposedTechnologies = r.ProposedTechnologies,
                Status = r.Status,
                RevisionCount = r.RevisionCount,
                PeriodName = r.RegistrationPeriod.Name,
                SubmittedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            };

            return new LecturerInboxViewModel
            {
                PendingProposals = allRegs.Where(r => r.Status == RegistrationStatus.PENDING).Select(Map).ToList(),
                RecentlyActioned = allRegs.Where(r => r.Status != RegistrationStatus.PENDING).Take(20).Select(Map).ToList(),
                TotalApprovedCount = allRegs.Count(r => r.Status == RegistrationStatus.APPROVED),
                MaxStudentsAllowed = lecturer?.MaxStudents ?? 0
            };
        }

        public async Task<ReviewDecisionViewModel?> GetProposalForReviewAsync(
            int registrationId, int lecturerProfileId)
        {
            var reg = await _db.Registrations
                .Include(r => r.Student).ThenInclude(s => s.User)
                .Include(r => r.RegistrationPeriod)
                .FirstOrDefaultAsync(r => r.Id == registrationId
                    && r.LecturerProfileId == lecturerProfileId);

            if (reg == null) return null;

            return new ReviewDecisionViewModel
            {
                RegistrationId = reg.Id,
                Proposal = new ProposalReviewItem
                {
                    RegistrationId = reg.Id,
                    StudentName = reg.Student.User.FullName,
                    StudentCode = reg.Student.StudentCode,
                    ProposedTitle = reg.ProposedTitle ?? string.Empty,
                    ProposedDescription = reg.ProposedDescription ?? string.Empty,
                    ProposedTechnologies = reg.ProposedTechnologies,
                    Status = reg.Status,
                    RevisionCount = reg.RevisionCount,
                    PeriodName = reg.RegistrationPeriod.Name,
                    SubmittedAt = reg.CreatedAt,
                    UpdatedAt = reg.UpdatedAt
                }
            };
        }

        public async Task<(bool Success, string Message)> ProcessDecisionAsync(
            int lecturerProfileId, ReviewDecisionViewModel vm)
        {
            var reg = await _db.Registrations
                .Include(r => r.Topic)
                .Include(r => r.Student).ThenInclude(s => s.User)
                .FirstOrDefaultAsync(r => r.Id == vm.RegistrationId
                    && r.LecturerProfileId == lecturerProfileId);

            if (reg == null)
                return (false, "Không tìm thấy đề xuất.");

            if (reg.Status != RegistrationStatus.PENDING)
                return (false, "Đề xuất này đã được xử lý rồi.");

            switch (vm.Decision)
            {
                case "approve":
                    reg.Status = RegistrationStatus.APPROVED;
                    reg.LecturerNote = vm.Note;
                    reg.Topic.Status = TopicStatus.Closed;

                    // Đồng bộ đề tài được duyệt lên sheet với ghi chú mã GV + thời gian
                    {
                        var lec = await _db.LecturerProfiles
                            .Include(l => l.User)
                            .FirstOrDefaultAsync(l => l.Id == lecturerProfileId);
                        var approveNote = $"Duyệt bởi {lec?.LecturerCode ?? "GV"} lúc {DateTime.Now:dd/MM/yyyy HH:mm}";

                        _db.TopicSyncRecords.Add(new TopicSyncRecord
                        {
                            TopicId = reg.TopicId,
                            PeriodId = reg.RegistrationPeriodId,
                            TopicTitle = reg.ProposedTitle ?? reg.Topic.Title,
                            TopicDescription = reg.ProposedDescription ?? reg.Topic.Description,
                            Technologies = reg.ProposedTechnologies,
                            MaxStudents = 1,
                            LecturerName = lec?.User?.FullName ?? string.Empty,
                            LecturerCode = lec?.LecturerCode ?? string.Empty,
                            Note = approveNote
                        });
                    }

                    await _notif.SendAsync(reg.Student.UserId,
                        "Đề xuất đề tài của bạn đã được DUYỆT! Chúc mừng.",
                        "/Student/Registration/MyRegistrations");

                    await TrySendDecisionEmailAsync(reg, TopicDecisionType.Approve, vm.Note, "/Student/Registration/MyRegistrations");
                    break;

                case "revise":
                    if (string.IsNullOrWhiteSpace(vm.Note))
                        return (false, "Vui lòng nhập yêu cầu chỉnh sửa cụ thể.");

                    reg.Status = RegistrationStatus.REVISION_REQUIRED;
                    reg.LecturerNote = vm.Note;

                    await _notif.SendAsync(reg.Student.UserId,
                        $"Giảng viên yêu cầu bạn chỉnh sửa đề xuất: {vm.Note}",
                        $"/Student/Registration/ReviseProposal/{reg.Id}");

                    await TrySendDecisionEmailAsync(reg, TopicDecisionType.Revise, vm.Note, $"/Student/Registration/ReviseProposal/{reg.Id}");
                    break;

                case "reject":
                    reg.Status = RegistrationStatus.REJECTED;
                    reg.LecturerNote = vm.Note;
                    reg.Topic.Status = TopicStatus.Closed;

                    await _notif.SendAsync(reg.Student.UserId,
                        "Đề xuất đề tài của bạn đã bị từ chối. Vui lòng chọn đề tài khác hoặc đề xuất lại.",
                        "/Student/Registration/TopicList");

                    await TrySendDecisionEmailAsync(reg, TopicDecisionType.Reject, vm.Note, "/Student/Registration/TopicList");
                    break;

                default:
                    return (false, "Quyết định không hợp lệ.");
            }

            reg.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return (true, "Đã xử lý thành công.");
        }

        // Helper: gửi email quyết định cho sinh viên, không throw nếu thất bại
        private async Task TrySendDecisionEmailAsync(
            Registration reg, TopicDecisionType decision, string? note, string actionPath)
        {
            var studentEmail = reg.Student?.User?.Email;
            if (string.IsNullOrEmpty(studentEmail))
            {
                _logger.LogWarning("Sinh viên {StudentId} không có email — bỏ qua gửi email quyết định.", reg.StudentProfileId);
                return;
            }

            try
            {
                await _email.SendTopicDecisionToStudentAsync(
                    toEmail: studentEmail,
                    studentName: reg.Student!.User.FullName ?? studentEmail,
                    topicTitle: reg.ProposedTitle ?? reg.Topic?.Title ?? string.Empty,
                    decision: decision,
                    reason: note,
                    actionUrl: actionPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email quyết định cho SV {StudentId}", reg.StudentProfileId);
            }
        }

        // ── Admin Export ──────────────────────────────────────────────────────
        public async Task<List<ExportRowViewModel>> GetExportDataAsync(int periodId)
        {
            return await _db.Registrations
                .Where(r => r.RegistrationPeriodId == periodId && r.Status == RegistrationStatus.APPROVED)
                .Include(r => r.Student).ThenInclude(s => s.User)
                .Include(r => r.Lecturer).ThenInclude(l => l.User)
                .Include(r => r.Topic)
                .Select(r => new ExportRowViewModel
                {
                    StudentCode = r.Student.StudentCode,
                    StudentName = r.Student.User.FullName,
                    TopicTitle = r.ProposedTitle ?? r.Topic.Title,
                    LecturerName = r.Lecturer.User.FullName,
                    Status = "APPROVED"
                })
                .ToListAsync();
        }

        public async Task<(int Created, int Skipped, List<string> Errors)> ImportTopicsFromFileAsync(int periodId, IFormFile file)
        {
            var period = await _db.RegistrationPeriods.FindAsync(periodId);
            if (period == null) return (0, 0, ["Không tìm thấy đợt đăng ký."]);

            var errors = new List<string>();
            var (rows, parseError) = await ParseTopicFileAsync(file);
            if (parseError != null && rows.Count == 0) return (0, 0, [parseError]);
            if (parseError != null) errors.Add(parseError);

            var lecturers = await _db.LecturerProfiles.Include(l => l.User).ToListAsync();
            var topicsToCreate = new List<(Topic topic, LecturerProfile lecturer)>();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                int rowNum = i + 2;

                if (string.IsNullOrWhiteSpace(row.LecturerCode) || string.IsNullOrWhiteSpace(row.Title))
                {
                    errors.Add($"Dòng {rowNum}: thiếu LecturerCode hoặc Title, bỏ qua.");
                    continue;
                }

                var code = row.LecturerCode.Trim();
                var lecturer = lecturers.FirstOrDefault(l =>
                    l.LecturerCode.Trim().Equals(code, StringComparison.OrdinalIgnoreCase) ||
                    (l.User?.Mssv != null && l.User.Mssv.Trim().Equals(code, StringComparison.OrdinalIgnoreCase)));

                if (lecturer == null)
                {
                    errors.Add($"Dòng {rowNum}: không tìm thấy giảng viên \"{row.LecturerCode.Trim()}\", bỏ qua.");
                    continue;
                }

                var topic = new Topic
                {
                    Title = row.Title.Trim(),
                    Description = string.IsNullOrWhiteSpace(row.Description) ? string.Empty : row.Description.Trim(),
                    Requirements = string.IsNullOrWhiteSpace(row.Requirements) ? null : row.Requirements.Trim(),
                    Technologies = string.IsNullOrWhiteSpace(row.Technologies) ? null : row.Technologies.Trim(),
                    MaxStudents = row.MaxStudents > 0 ? row.MaxStudents : 1,
                    Status = TopicStatus.Open,
                    LecturerProfileId = lecturer.Id,
                    RegistrationPeriodId = periodId,
                    Note = string.IsNullOrWhiteSpace(row.Note) ? null : row.Note.Trim()
                };
                topicsToCreate.Add((topic, lecturer));
            }

            int skipped = rows.Count - topicsToCreate.Count;
            if (topicsToCreate.Count == 0) return (0, skipped, errors);

            _db.Topics.AddRange(topicsToCreate.Select(x => x.topic));
            await _db.SaveChangesAsync();

            _db.TopicSyncRecords.AddRange(topicsToCreate.Select(x => new TopicSyncRecord
            {
                TopicId = x.topic.Id,
                PeriodId = periodId,
                TopicTitle = x.topic.Title,
                TopicDescription = x.topic.Description,
                Technologies = x.topic.Technologies,
                Requirements = x.topic.Requirements,
                MaxStudents = x.topic.MaxStudents,
                LecturerName = x.lecturer.User?.FullName ?? string.Empty,
                LecturerCode = x.lecturer.LecturerCode,
                Note = x.topic.Note
            }));
            await _db.SaveChangesAsync();

            return (topicsToCreate.Count, skipped, errors);
        }

        private static async Task<(List<TopicImportRow> Rows, string? Error)> ParseTopicFileAsync(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var rows = new List<TopicImportRow>();

            if (ext == ".xlsx")
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var ws = workbook.Worksheets.First();
                int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                for (int r = 2; r <= lastRow; r++)
                {
                    var row = ws.Row(r);
                    if (row.IsEmpty()) continue;
                    rows.Add(new TopicImportRow(
                        row.Cell(1).GetString().Trim(),
                        row.Cell(2).GetString().Trim(),
                        row.Cell(3).GetString().Trim(),
                        row.Cell(4).GetString().Trim(),
                        row.Cell(5).GetString().Trim(),
                        int.TryParse(row.Cell(6).GetString().Trim(), out var ms) ? ms : 1,
                        row.Cell(7).GetString().Trim()
                    ));
                }
            }
            else if (ext is ".csv" or ".txt")
            {
                using var reader = new StreamReader(file.OpenReadStream());
                string? line;
                bool isHeader = true;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (isHeader) { isHeader = false; continue; }
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');
                    rows.Add(new TopicImportRow(
                        GetCsvCell(parts, 0),
                        GetCsvCell(parts, 1),
                        GetCsvCell(parts, 2),
                        GetCsvCell(parts, 3),
                        GetCsvCell(parts, 4),
                        int.TryParse(GetCsvCell(parts, 5), out var ms) ? ms : 1,
                        GetCsvCell(parts, 6)
                    ));
                }
            }
            else
            {
                return (rows, "Định dạng file không hỗ trợ. Vui lòng dùng .xlsx, .csv hoặc .txt.");
            }

            return (rows, null);
        }

        // ── Lecturer: Bulk Import Own Topics from Excel ───────────────────────
        // Cột file: 1=Title*, 2=Description, 3=Requirements, 4=Technologies, 5=MaxStudents, 6=Note
        // Tự gán giảng viên đang đăng nhập + đợt được chọn. Toàn bộ dòng hợp lệ lưu trong 1 transaction.
        public async Task<(int Created, int Skipped, List<string> Errors)> ImportLecturerTopicsAsync(
            int lecturerProfileId, int periodId, IFormFile file)
        {
            var errors = new List<string>();

            var now = DateTime.UtcNow;
            var period = await _db.RegistrationPeriods.FindAsync(periodId);
            if (period == null || !(period.IsActive && period.StartDate <= now && period.EndDate >= now))
                return (0, 0, new List<string> { "Đợt đăng ký không tồn tại hoặc đã đóng — không thể import." });

            var lecturer = await _db.LecturerProfiles
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == lecturerProfileId);
            if (lecturer == null)
                return (0, 0, new List<string> { "Không tìm thấy hồ sơ giảng viên." });

            List<ExcelRow> rows;
            try
            {
                rows = _excel.ReadRows(file, 6);
            }
            catch (ExcelReadException ex)
            {
                return (0, 0, new List<string> { ex.Message });
            }

            if (rows.Count == 0)
                return (0, 0, new List<string> { "File không có dòng dữ liệu nào (chỉ có tiêu đề)." });

            // ReadCommitted + UPDLOCK/HOLDLOCK trên phạm vi đề tài của đợt:
            // serialize các lần import đồng thời cho CÙNG một đợt → tránh trùng tên do race,
            // mà không khóa toàn bảng Topics của các đợt khác.
            using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                var existingTitles = (await _db.Topics
                        .FromSqlRaw("SELECT * FROM Topics WITH (UPDLOCK, HOLDLOCK) WHERE RegistrationPeriodId = {0}", periodId)
                        .ToListAsync())
                    .Select(t => t.Title.Trim().ToLowerInvariant())
                    .ToHashSet();

                var toCreate = new List<Topic>();
                var seenInFile = new HashSet<string>();

                foreach (var row in rows)
                {
                    var title = row.Get(0).Trim();
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        errors.Add($"Dòng {row.RowNumber}: thiếu Tên đề tài, bỏ qua.");
                        continue;
                    }
                    if (title.Length > 300)
                    {
                        errors.Add($"Dòng {row.RowNumber}: Tên đề tài vượt quá 300 ký tự, bỏ qua.");
                        continue;
                    }

                    var key = title.ToLowerInvariant();
                    if (existingTitles.Contains(key))
                    {
                        errors.Add($"Dòng {row.RowNumber}: đề tài \"{title}\" đã tồn tại trong đợt này, bỏ qua.");
                        continue;
                    }
                    if (!seenInFile.Add(key))
                    {
                        errors.Add($"Dòng {row.RowNumber}: đề tài \"{title}\" bị lặp lại trong file, bỏ qua.");
                        continue;
                    }

                    var maxRaw = row.Get(4).Trim();
                    int maxStudents = 1;
                    if (!string.IsNullOrWhiteSpace(maxRaw) &&
                        (!int.TryParse(maxRaw, out maxStudents) || maxStudents < 1 || maxStudents > 10))
                    {
                        errors.Add($"Dòng {row.RowNumber}: Số SV tối đa \"{maxRaw}\" không hợp lệ (1–10), bỏ qua.");
                        continue;
                    }

                    var description = row.Get(1).Trim();
                    var requirements = Truncate(row.Get(2).Trim(), 500);
                    var technologies = Truncate(row.Get(3).Trim(), 300);
                    var note = Truncate(row.Get(5).Trim(), 500);

                    toCreate.Add(new Topic
                    {
                        Title = title,
                        Description = string.IsNullOrWhiteSpace(description) ? title : description,
                        Requirements = string.IsNullOrWhiteSpace(requirements) ? null : requirements,
                        Technologies = string.IsNullOrWhiteSpace(technologies) ? null : technologies,
                        MaxStudents = maxStudents,
                        Status = TopicStatus.Open,
                        LecturerProfileId = lecturerProfileId,
                        RegistrationPeriodId = periodId,
                        Note = string.IsNullOrWhiteSpace(note) ? null : note
                    });
                }

                int skipped = rows.Count - toCreate.Count;
                if (toCreate.Count == 0)
                {
                    await tx.RollbackAsync();
                    return (0, skipped, errors);
                }

                _db.Topics.AddRange(toCreate);
                await _db.SaveChangesAsync();

                // Outbox: queue đề tài lên Google Sheets giống CreateTopicAsync
                _db.TopicSyncRecords.AddRange(toCreate.Select(t => new TopicSyncRecord
                {
                    TopicId = t.Id,
                    PeriodId = periodId,
                    TopicTitle = t.Title,
                    TopicDescription = t.Description,
                    Technologies = t.Technologies,
                    Requirements = t.Requirements,
                    MaxStudents = t.MaxStudents,
                    LecturerName = lecturer.User?.FullName ?? string.Empty,
                    LecturerCode = lecturer.LecturerCode,
                    Note = t.Note
                }));
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
                return (toCreate.Count, skipped, errors);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        private static string Truncate(string value, int max) =>
            value.Length > max ? value[..max] : value;

        private static string GetCsvCell(string[] parts, int idx) =>
            idx < parts.Length ? parts[idx].Trim().Trim('"') : string.Empty;

        private record TopicImportRow(
            string LecturerCode,
            string Title,
            string Description,
            string? Requirements,
            string? Technologies,
            int MaxStudents,
            string? Note
        );
    }

}
