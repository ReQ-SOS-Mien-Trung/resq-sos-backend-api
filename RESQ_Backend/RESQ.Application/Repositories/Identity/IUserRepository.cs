using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.Repositories.Identity
{
    public interface IUserRepository
    {
        Task<UserModel?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
        Task<UserModel?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<UserModel?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default);
        Task<UserModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<UserModel?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken = default);
        Task<UserModel?> GetByPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default);
        Task CreateAsync(UserModel user, CancellationToken cancellationToken = default);
        Task UpdateAsync(UserModel user, CancellationToken cancellationToken = default);
        Task<PagedResult<UserModel>> GetPagedAsync(int pageNumber, int pageSize, int? roleId = null, bool? isBanned = null, string? search = null, CancellationToken cancellationToken = default);
    }
}
