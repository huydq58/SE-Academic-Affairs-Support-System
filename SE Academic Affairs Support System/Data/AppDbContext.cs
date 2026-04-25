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
        public DbSet<AppRegistrationRequest> AppRegistrationRequests { get; set; }

        public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
        public DbSet<LecturerProfile> LecturerProfiles => Set<LecturerProfile>();
        public DbSet<RegistrationPeriod> RegistrationPeriods => Set<RegistrationPeriod>();
        public DbSet<Topic> Topics => Set<Topic>();
        public DbSet<Registration> Registrations => Set<Registration>();
        public DbSet<Notification> Notifications => Set<Notification>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();


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
        }
    }
    
}