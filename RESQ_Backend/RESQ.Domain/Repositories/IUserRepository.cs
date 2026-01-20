using System;
using System.Threading.Tasks;
using RESQ.Domain.Models;

namespace RESQ.Domain.Repositories
{
    public interface IUserRepository
    {
        Task<UserModel?> GetByUsernameAsync(string username);
        Task<UserModel?> GetByIdAsync(Guid id);
        Task CreateAsync(UserModel user);
        Task UpdateAsync(UserModel user);
    }
}
