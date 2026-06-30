using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.Data
{
    public class AppDbContext : IdentityDbContext<User>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<RoomModel> Rooms { get; set; }
        public DbSet<TimeSlot> TimeSlots { get; set; }
        public DbSet<RoomBooking> RoomBookings { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<DeviceRequest> DeviceRequests { get; set; }
        public DbSet<DeviceRequestItem> DeviceRequestItems { get; set; }
        public DbSet<DeviceDamageReport> DeviceDamageReports { get; set; }
        public DbSet<AppRegistrationRequest> AppRegistrationRequests { get; set; }

        public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
        public DbSet<LecturerProfile> LecturerProfiles => Set<LecturerProfile>();
        public DbSet<RegistrationPeriod> RegistrationPeriods => Set<RegistrationPeriod>();
        public DbSet<Topic> Topics => Set<Topic>();
        public DbSet<Registration> Registrations => Set<Registration>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<TopicRegistration> TopicRegistrations { get; set; }
        public DbSet<GradeRecord> GradeRecords { get; set; }
        public DbSet<TopicSyncRecord> TopicSyncRecords { get; set; }
        public DbSet<RegistrationPeriodStudent> RegistrationPeriodStudents => Set<RegistrationPeriodStudent>();
        public DbSet<EmailConfiguration> EmailConfigurations => Set<EmailConfiguration>();
        public DbSet<ReportSubmission> ReportSubmissions => Set<ReportSubmission>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Mssv unique nhưng chỉ khi không null (Admin/Lecturer có Mssv = null)
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Mssv)
                .IsUnique()
                .HasFilter("[Mssv] IS NOT NULL");


            // DeviceCode unique nhưng chỉ khi không null (thiết bị tạo thủ công có thể chưa có mã)
            modelBuilder.Entity<Device>()
                .HasIndex(d => d.DeviceCode)
                .IsUnique()
                .HasFilter("[DeviceCode] IS NOT NULL");

            // Phiếu mượn 1-nhiều chi tiết mượn
            modelBuilder.Entity<DeviceRequestItem>(e =>
            {
                e.HasOne(i => i.Request)
                    .WithMany(r => r.Items)
                    .HasForeignKey(i => i.RequestId)
                    .OnDelete(DeleteBehavior.Cascade);   // xóa phiếu → xóa chi tiết

                e.HasOne(i => i.Device)
                    .WithMany()
                    .HasForeignKey(i => i.DeviceId)
                    .OnDelete(DeleteBehavior.Restrict);  // không cho xóa thiết bị đang nằm trong phiếu
            });

            // Bài nộp báo cáo — 1 SV ↔ 1 bài / đợt; NoAction tránh cascade cycle
            modelBuilder.Entity<ReportSubmission>(e =>
            {
                e.HasIndex(s => new { s.StudentProfileId, s.RegistrationPeriodId }).IsUnique();
                e.HasOne(s => s.Student)
                    .WithMany()
                    .HasForeignKey(s => s.StudentProfileId)
                    .OnDelete(DeleteBehavior.NoAction);
                e.HasOne(s => s.Period)
                    .WithMany()
                    .HasForeignKey(s => s.RegistrationPeriodId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // Báo cáo hư hỏng — NoAction để tránh cascade cycle
            modelBuilder.Entity<DeviceDamageReport>(e =>
            {
                e.HasOne(r => r.Request)
                    .WithMany()
                    .HasForeignKey(r => r.RequestId)
                    .OnDelete(DeleteBehavior.NoAction);

                e.HasOne(r => r.Device)
                    .WithMany()
                    .HasForeignKey(r => r.DeviceId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // Unique student code
            modelBuilder.Entity<StudentProfile>()
                .HasIndex(s => s.StudentCode).IsUnique();

            // Unique lecturer code
            modelBuilder.Entity<LecturerProfile>()
                .HasIndex(l => l.LecturerCode).IsUnique();

            // One student cannot register the same topic twice in the same period
            modelBuilder.Entity<Registration>()
                .HasIndex(r => new { r.StudentProfileId, r.TopicId, r.RegistrationPeriodId })
                .IsUnique();

            // Prevent cascade delete cycles
            modelBuilder.Entity<Registration>()
                .HasOne(r => r.Lecturer)
                .WithMany(l => l.Registrations)
                .HasForeignKey(r => r.LecturerProfileId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Registration>()
                .HasOne(r => r.Topic)
                .WithMany(t => t.Registrations)
                .HasForeignKey(r => r.TopicId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Topic>()
                .HasOne(t => t.ProposedByStudent)
                .WithMany()
                .HasForeignKey(t => t.ProposedByStudentId)
                .OnDelete(DeleteBehavior.NoAction);

            // Junction table: đợt đăng ký ↔ sinh viên được phép
            // TopicSyncRecord FK — no cascade để tránh cycle
            modelBuilder.Entity<TopicSyncRecord>()
                .HasOne(r => r.Topic)
                .WithMany()
                .HasForeignKey(r => r.TopicId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TopicSyncRecord>()
                .HasOne(r => r.Period)
                .WithMany()
                .HasForeignKey(r => r.PeriodId)
                .OnDelete(DeleteBehavior.NoAction);

            // GradeRecord.Score precision
            modelBuilder.Entity<GradeRecord>()
                .Property(g => g.Score)
                .HasPrecision(5, 2);

            modelBuilder.Entity<RegistrationPeriodStudent>(e =>
            {
                e.HasOne(rps => rps.RegistrationPeriod)
                    .WithMany(rp => rp.AllowedStudents)
                    .HasForeignKey(rps => rps.RegistrationPeriodId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(rps => rps.StudentProfile)
                    .WithMany()
                    .HasForeignKey(rps => rps.StudentProfileId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Một sinh viên chỉ xuất hiện một lần trong mỗi đợt
                e.HasIndex(rps => new { rps.RegistrationPeriodId, rps.StudentProfileId })
                    .IsUnique();
            });
        }
    }

}