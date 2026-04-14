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
        Task<List<UserModel>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
        Task<UserModel?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken = default);
        Task<UserModel?> GetByPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default);
        Task CreateAsync(UserModel user, CancellationToken cancellationToken = default);
        Task UpdateAsync(UserModel user, CancellationToken cancellationToken = default);
        Task<PagedResult<UserModel>> GetPagedAsync(int pageNumber, int pageSize, int? roleId = null, bool? isBanned = null, string? search = null, int? excludeRoleId = null, bool? isEligible = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// L?y user cho trang phân quy?n admin: lo?i tr? user b? ban và nh?ng
        /// volunteer chua kích ho?t (IsEligibleRescuer = false).
        /// </summary>
        Task<PagedResult<UserModel>> GetPagedForPermissionAsync(int pageNumber, int pageSize, int? roleId = null, string? search = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// L?y danh sách Id c?a t?t c? admin dang ho?t d?ng (không b? ban), dùng d? g?i thông báo h? th?ng.
        /// </summary>
        Task<List<Guid>> GetActiveAdminUserIdsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// L?y danh sách Manager (RoleId=4, không b? ban) chua du?c gán qu?n lý kho nào (không có b?n ghi
        /// depot_managers v?i UnassignedAt = null). Dùng cho dropdown ch?n manager khi t?o/gán kho.
        /// </summary>
        Task<List<AvailableManagerDto>> GetAvailableManagersAsync(CancellationToken cancellationToken = default);
    }
}
