using System;
using BCrypt.Net;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using Microsoft.EntityFrameworkCore;
namespace SE_Academic_Affairs_Support_System.Services
{
        public interface IAuthService
        {
            Task<User?> ValidateUserAsync(string email, string password);
            Task<bool> RegisterUserAsync(string fullName, string email, string password);
        }

        public class AuthService : IAuthService
        {
            private readonly AppDbContext _context;

            public AuthService(AppDbContext context)
            {
                _context = context;
            }

            public async Task<User?> ValidateUserAsync(string email, string password)
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email.ToLower());

                if (user == null) return null;

                bool isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                return isValid ? user : null;
            }

            public async Task<bool> RegisterUserAsync(string fullName, string email, string password)
            {
                bool exists = await _context.Users.AnyAsync(u => u.Email == email.ToLower());
                if (exists) return false;

                var user = new User
                {
                    FullName = fullName,
                    Email = email.ToLower(),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return true;
            }
        }
    
}
