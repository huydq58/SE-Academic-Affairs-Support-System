using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.ViewModels;

namespace SE_Academic_Affairs_Support_System.Services.EmailConfig
{
    public class EmailConfigurationService : IEmailConfigurationService
    {
        private readonly AppDbContext _db;
        private readonly IDataProtector _protector;

        public EmailConfigurationService(AppDbContext db, IDataProtectionProvider dpProvider)
        {
            _db = db;
            _protector = dpProvider.CreateProtector("EmailConfig.AppPassword");
        }

        public async Task<List<EmailConfigListItemViewModel>> GetAllAsync()
        {
            return await _db.EmailConfigurations
                .OrderByDescending(e => e.IsActive)
                .ThenByDescending(e => e.CreatedAt)
                .Select(e => new EmailConfigListItemViewModel
                {
                    Id = e.Id,
                    DisplayName = e.DisplayName,
                    SenderEmail = e.SenderEmail,
                    SenderName = e.SenderName,
                    SmtpHost = e.SmtpHost,
                    SmtpPort = e.SmtpPort,
                    EnableSsl = e.EnableSsl,
                    IsActive = e.IsActive,
                    CreatedAt = e.CreatedAt,
                    UpdatedAt = e.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<EmailConfigFormViewModel?> GetForEditAsync(int id)
        {
            var e = await _db.EmailConfigurations.FindAsync(id);
            if (e == null) return null;
            return new EmailConfigFormViewModel
            {
                Id = e.Id,
                DisplayName = e.DisplayName,
                SenderEmail = e.SenderEmail,
                SenderName = e.SenderName,
                SmtpHost = e.SmtpHost,
                SmtpPort = e.SmtpPort,
                EnableSsl = e.EnableSsl
                // AppPassword intentionally left blank — never sent to client
            };
        }

        public async Task<EmailConfiguration?> GetActiveAsync()
        {
            return await _db.EmailConfigurations.FirstOrDefaultAsync(e => e.IsActive);
        }

        public async Task<EmailConfiguration?> GetActiveEntityByIdAsync(int id)
        {
            return await _db.EmailConfigurations.FindAsync(id);
        }

        public async Task<(bool Success, string Message)> CreateAsync(EmailConfigFormViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.AppPassword))
                return (false, "App Password là bắt buộc khi tạo mới cấu hình.");

            var entity = new EmailConfiguration
            {
                DisplayName = vm.DisplayName,
                SenderEmail = vm.SenderEmail,
                SenderName = vm.SenderName,
                EncryptedAppPassword = _protector.Protect(vm.AppPassword),
                SmtpHost = vm.SmtpHost,
                SmtpPort = vm.SmtpPort,
                EnableSsl = vm.EnableSsl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.EmailConfigurations.Add(entity);
            await _db.SaveChangesAsync();
            return (true, "Đã thêm cấu hình email.");
        }

        public async Task<(bool Success, string Message)> UpdateAsync(EmailConfigFormViewModel vm)
        {
            var entity = await _db.EmailConfigurations.FindAsync(vm.Id);
            if (entity == null) return (false, "Không tìm thấy cấu hình.");

            entity.DisplayName = vm.DisplayName;
            entity.SenderEmail = vm.SenderEmail;
            entity.SenderName = vm.SenderName;
            entity.SmtpHost = vm.SmtpHost;
            entity.SmtpPort = vm.SmtpPort;
            entity.EnableSsl = vm.EnableSsl;
            entity.UpdatedAt = DateTime.UtcNow;

            // Only update password if a new one was provided
            if (!string.IsNullOrWhiteSpace(vm.AppPassword))
                entity.EncryptedAppPassword = _protector.Protect(vm.AppPassword);

            await _db.SaveChangesAsync();
            return (true, "Đã cập nhật cấu hình email.");
        }

        public async Task<(bool Success, string Message)> DeleteAsync(int id)
        {
            var entity = await _db.EmailConfigurations.FindAsync(id);
            if (entity == null) return (false, "Không tìm thấy cấu hình.");
            if (entity.IsActive) return (false, "Không thể xóa cấu hình đang Active. Hãy đặt cấu hình khác làm Active trước.");

            _db.EmailConfigurations.Remove(entity);
            await _db.SaveChangesAsync();
            return (true, "Đã xóa cấu hình email.");
        }

        public async Task<(bool Success, string Message)> SetActiveAsync(int id)
        {
            var target = await _db.EmailConfigurations.FindAsync(id);
            if (target == null) return (false, "Không tìm thấy cấu hình.");

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                await _db.EmailConfigurations
                    .Where(e => e.IsActive)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsActive, false));

                target.IsActive = true;
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return (true, $"Đã đặt \"{target.DisplayName}\" làm cấu hình active.");
            }
            catch
            {
                await tx.RollbackAsync();
                return (false, "Có lỗi xảy ra khi cập nhật.");
            }
        }

    }
}
