using Microsoft.EntityFrameworkCore;
using RESQ.Domain.Models;
using RESQ.Domain.Entities;
using RESQ.Domain.Repositories;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Mappers.Users;
using System.Threading.Tasks;
using System;

namespace RESQ.Infrastructure.Persistence.Users
{
    public class UserRepository : IUserRepository
    {
        private readonly ResQDbContext _context;

        public UserRepository(ResQDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(UserModel user)
        {
            var dbUser = new User
            {
                Id = user.Id == Guid.Empty ? Guid.NewGuid() : user.Id,
                RoleId = user.RoleId,
                FullName = user.FullName,
                Username = user.Username,
                Phone = user.Phone,
                Password = user.Password,
                CreatedAt = user.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = user.UpdatedAt ?? DateTime.UtcNow,
                RefreshToken = user.RefreshToken,
                RefreshTokenExpiry = user.RefreshTokenExpiry
            };

            _context.Users.Add(dbUser);
            await _context.SaveChangesAsync();
        }

        public async Task<UserModel?> GetByIdAsync(Guid id)
        {
            var db = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            return db == null ? null : db.ToDomain();
        }

        public async Task<UserModel?> GetByUsernameAsync(string username)
        {
            var db = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            return db == null ? null : db.ToDomain();
        }

        public async Task UpdateAsync(UserModel user)
        {
            var db = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            if (db == null) return;
            db.UpdateDb(user);
            await _context.SaveChangesAsync();
        }
    }
}
