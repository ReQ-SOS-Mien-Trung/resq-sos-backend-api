using RESQ.Domain.Entities.Users;

namespace RESQ.Domain.Repositories
{
    public interface IUserRepository
    {
        Task<UserModel?> GetByUsernameAsync(string username);
        Task<UserModel?> GetByPhoneAsync(string phone);
        Task<UserModel?> GetByIdAsync(Guid id);
        Task CreateAsync(UserModel user);
        Task UpdateAsync(UserModel user);
    }
}
