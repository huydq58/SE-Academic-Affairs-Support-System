using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Helper;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.Services.Email;
using SE_Academic_Affairs_Support_System.Services.EmailNotification;
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

        public RegistrationService(
            AppDbContext db,
            INotificationService notif,
            GoogleSheetsService sheets,
            IEmailService email,
            IEmailNotificationService emailNotif,
            ILogger<RegistrationService> logger)
        {
            _db = db;
            _notif = notif;
            _sheets = sheets;
            _email = email;
            _emailNotif = emailNotif;
            _logger = logger;
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
            _db.RegistrationPeriods.Add(new RegistrationPeriod
            {
                Name = vm.Name,
                CourseName = vm.CourseName,
                GoogleSheetLink = vm.GoogleSheetLink,
                StartDate = vm.StartDate,
                EndDate = vm.EndDate,
                IsActive = vm.IsActive,
                RestrictToAllowedStudents = vm.RestrictToAllowedStudents
            });
            await _db.SaveChangesAsync();
        }

        public async Task UpdatePeriodAsync(PeriodFormViewModel vm)
        {
            var period = await _db.RegistrationPeriods.FindAsync(vm.Id)
                         ?? throw new InvalidOperationException("Không tìm thấy đợt đăng ký");
            period.Name = vm.Name;
            period.CourseName = vm.CourseName;
            period.StartDate = vm.StartDate;
            period.EndDate = vm.EndDate;
            period.IsActive = vm.IsActive;
            period.RestrictToAllowedStudents = vm.RestrictToAllowedStudents;
            await _db.SaveChangesAsync();
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

            // Ưu tiên load từ Google Sheet nếu đợt đã được gắn sheet link
            var sheetId = GoogleSheetHelper.ExtractSheetId(period.GoogleSheetLink);
            if (sheetId != null)
            {
                List<TopicSheet> sheetTopics;
                try { sheetTopics = await _sheets.GetTopicsAsync(sheetId); }
                catch { sheetTopics = []; }

                // Chỉ lấy đề tài còn chỗ (Registered < MaxSlot)
                var available = sheetTopics.Where(t => t.Registered < t.MaxSlot).ToList();

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var kw = keyword.Trim();
                    available = available.Where(t =>
                        t.TopicName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                        t.Description.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                        t.LecturerInfo.Contains(kw, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                var cards = available.Select(t => new TopicCardViewModel
                {
                    TopicId = t.TopicId,
                    Title = t.TopicName,
                    Description = t.Description,
                    Requirements = t.Requirements,
                    Technologies = t.Technologies,
                    LecturerName = t.Lecturer,
                    LecturerProfileId = 0,          // không dùng cho filter khi có sheet
                    MaxStudents = t.MaxSlot,
                    RegisteredCount = t.Registered,
                    AlreadyRegistered = registeredTopicIds.Contains(t.TopicId)
                }).ToList();

                return new TopicListViewModel
                {
                    Period = period,
                    Topics = cards,
                    SearchKeyword = keyword,
                    FilterLecturerId = null,
                    Lecturers = []
                };
            }

            // Fallback: load từ DB
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

            // Serializable transaction để ngăn race condition (hai SV đăng ký cùng lúc)
            using var tx = await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);
            try
            {
                var topic = await _db.Topics
                    .Include(t => t.Registrations)
                    .Include(t => t.Lecturer).ThenInclude(l => l.User)
                    .FirstOrDefaultAsync(t => t.Id == topicId);

                if (topic == null || topic.Status != TopicStatus.Open)
                    return (false, "Đề tài không tồn tại hoặc đã đóng đăng ký.");

                // Check slot
                int approvedCount = topic.Registrations.Count(r => r.Status == RegistrationStatus.APPROVED);
                if (approvedCount >= topic.MaxStudents)
                    return (false, "Đề tài đã hết chỗ.");

                // Check duplicate
                bool alreadyRegistered = await _db.Registrations
                    .AnyAsync(r => r.StudentProfileId == studentProfileId
                                && r.TopicId == topicId
                                && r.Status != RegistrationStatus.REJECTED);
                if (alreadyRegistered)
                    return (false, "Bạn đã đăng ký đề tài này rồi.");

                // Check if student already has an approved registration this period
                bool hasApproved = await _db.Registrations
                    .AnyAsync(r => r.StudentProfileId == studentProfileId
                                && r.RegistrationPeriodId == period.Id
                                && r.Status == RegistrationStatus.APPROVED);
                if (hasApproved)
                    return (false, "Bạn đã có đề tài được duyệt trong đợt này.");

                var student = await _db.StudentProfiles
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.Id == studentProfileId);

                _db.Registrations.Add(new Registration
                {
                    StudentProfileId = studentProfileId,
                    TopicId = topicId,
                    LecturerProfileId = topic.LecturerProfileId,
                    RegistrationPeriodId = period.Id,
                    Status = RegistrationStatus.APPROVED,
                    UpdatedAt = DateTime.UtcNow
                });

                // Close topic if now full
                if (approvedCount + 1 >= topic.MaxStudents)
                    topic.Status = TopicStatus.Closed;

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                // Cập nhật sheet nếu topic đã được sync (có SheetRowIndex)
                if (topic.SheetRowIndex.HasValue)
                {
                    var sheetId = GoogleSheetHelper.ExtractSheetId(period.GoogleSheetLink);
                    if (sheetId != null && student != null)
                    {
                        try
                        {
                            await _sheets.RegisterAsync(new RegisterTopicRequest
                            {
                                SheetId = sheetId,
                                RowIndex = topic.SheetRowIndex.Value,
                                StudentId = student.StudentCode,
                                StudentName = student.User.FullName
                            });
                        }
                        catch { /* sheet update không ảnh hưởng nghiệp vụ DB */ }
                    }
                }

                if (student?.User?.Email != null)
                {
                    var lecturerName = topic.Lecturer?.User?.FullName ?? topic.Lecturer?.LecturerCode ?? string.Empty;
                    await _emailNotif.NotifyTopicRegisteredAsync(
                        student.User.Email,
                        student.User.FullName ?? student.StudentCode,
                        topic.Title,
                        lecturerName,
                        period.Name);
                }

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
    }

}
