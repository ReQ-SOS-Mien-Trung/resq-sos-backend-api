using RESQ.Domain.Entities;

namespace RESQ.Application.Repositories.Users
{
    public interface IUserRepository
    {
        Task<UserModel?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
        Task<UserModel?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default);
        Task<UserModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task CreateAsync(UserModel user, CancellationToken cancellationToken = default);
        Task UpdateAsync(UserModel user, CancellationToken cancellationToken = default);
    }
}
