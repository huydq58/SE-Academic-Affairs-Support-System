using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.ViewModels;

namespace SE_Academic_Affairs_Support_System.Services.AccountManagement
{
    public class AccountService : IAccountService
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _db;

        public AccountService(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, AppDbContext db)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
        }

        public async Task<UserListViewModel> GetUsersAsync(string? keyword, string? roleFilter)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(u =>
                    (u.FullName != null && u.FullName.ToLower().Contains(kw)) ||
                    (u.Email != null && u.Email.ToLower().Contains(kw)) ||
                    (u.Mssv != null && u.Mssv.ToLower().Contains(kw)));
            }

            if (!string.IsNullOrWhiteSpace(roleFilter))
                query = query.Where(u => u.Role == roleFilter);

            var users = await query.OrderBy(u => u.FullName).ToListAsync();

            var rows = users.Select(u => new UserRowViewModel
            {
                Id = u.Id,
                FullName = u.FullName ?? u.UserName ?? string.Empty,
                Email = u.Email ?? string.Empty,
                Role = u.Role ?? string.Empty,
                Mssv = u.Mssv,
                CreatedAt = u.CreatedAt
            }).ToList();

            return new UserListViewModel { Users = rows, SearchKeyword = keyword, RoleFilter = roleFilter };
        }

        public async Task<UserFormViewModel?> GetUserForEditAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return null;

            return new UserFormViewModel
            {
                Id = user.Id,
                FullName = user.FullName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Role = user.Role ?? "Student",
                Mssv = user.Mssv
            };
        }

        public async Task<(bool Success, string Message)> CreateUserAsync(UserFormViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Password))
                return (false, "Mật khẩu không được để trống khi tạo tài khoản mới.");

            // Ensure role exists
            if (!await _roleManager.RoleExistsAsync(vm.Role))
                await _roleManager.CreateAsync(new IdentityRole(vm.Role));

            var user = new User
            {
                UserName = vm.Email,
                Email = vm.Email,
                FullName = vm.FullName,
                Role = vm.Role,
                Mssv = string.IsNullOrWhiteSpace(vm.Mssv) ? null : vm.Mssv.Trim(),
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, vm.Password);
            if (!result.Succeeded)
                return (false, string.Join("; ", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, vm.Role);

            // Tạo profile tương ứng
            if (vm.Role == "Student")
            {
                var mssv = string.IsNullOrWhiteSpace(vm.Mssv)
                    ? "SV" + DateTime.Now.Ticks.ToString()[10..]
                    : vm.Mssv.Trim();
                _db.StudentProfiles.Add(new StudentProfile { UserId = user.Id, StudentCode = mssv });
            }
            else if (vm.Role == "Lecturer")
            {
                var code = string.IsNullOrWhiteSpace(vm.Mssv)
                    ? "GV" + DateTime.Now.Ticks.ToString()[10..]
                    : vm.Mssv.Trim();
                _db.LecturerProfiles.Add(new LecturerProfile { UserId = user.Id, LecturerCode = code, MaxStudents = 10 });
            }

            await _db.SaveChangesAsync();
            return (true, $"Tạo tài khoản \"{vm.Email}\" thành công.");
        }

        public async Task<(bool Success, string Message)> UpdateUserAsync(UserFormViewModel vm)
        {
            if (string.IsNullOrEmpty(vm.Id))
                return (false, "Không tìm thấy tài khoản.");

            var user = await _userManager.FindByIdAsync(vm.Id);
            if (user == null) return (false, "Tài khoản không tồn tại.");

            // Lấy phần trước @ làm username
            var atIndex = vm.Email.IndexOf('@');
            var userName = atIndex > 0 ? vm.Email[..atIndex] : vm.Email;

            user.FullName = vm.FullName;
            user.Email = vm.Email;
            user.UserName = userName;
            user.NormalizedEmail = vm.Email.ToUpperInvariant();
            user.NormalizedUserName = userName.ToUpperInvariant();
            user.Role = vm.Role;
            user.Mssv = string.IsNullOrWhiteSpace(vm.Mssv) ? null : vm.Mssv.Trim();

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return (false, string.Join("; ", updateResult.Errors.Select(e => e.Description)));

            // Sync profile code khi Mssv thay đổi
            if (!string.IsNullOrWhiteSpace(vm.Mssv))
            {
                var newCode = vm.Mssv.Trim();
                var lecturerProfile = await _db.LecturerProfiles.FirstOrDefaultAsync(l => l.UserId == user.Id);
                if (lecturerProfile != null && lecturerProfile.LecturerCode != newCode)
                {
                    lecturerProfile.LecturerCode = newCode;
                    await _db.SaveChangesAsync();
                }
                var studentProfile = await _db.StudentProfiles.FirstOrDefaultAsync(s => s.UserId == user.Id);
                if (studentProfile != null && studentProfile.StudentCode != newCode)
                {
                    studentProfile.StudentCode = newCode;
                    await _db.SaveChangesAsync();
                }
            }

            if (!string.IsNullOrWhiteSpace(vm.Password))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var pwResult = await _userManager.ResetPasswordAsync(user, token, vm.Password);
                if (!pwResult.Succeeded)
                    return (false, "Cập nhật thành công nhưng không đổi được mật khẩu: " +
                        string.Join("; ", pwResult.Errors.Select(e => e.Description)));
            }

            return (true, "Cập nhật tài khoản thành công.");
        }

        public async Task<(bool Success, string Message)> DeleteUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return (false, "Tài khoản không tồn tại.");

            // Xóa profile liên kết
            var studentProfile = await _db.StudentProfiles.FirstOrDefaultAsync(s => s.UserId == userId);
            if (studentProfile != null) _db.StudentProfiles.Remove(studentProfile);

            var lecturerProfile = await _db.LecturerProfiles.FirstOrDefaultAsync(l => l.UserId == userId);
            if (lecturerProfile != null) _db.LecturerProfiles.Remove(lecturerProfile);

            await _db.SaveChangesAsync();

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                return (false, string.Join("; ", result.Errors.Select(e => e.Description)));

            return (true, "Đã xóa tài khoản.");
        }
    }
}
