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
        /// Lấy user cho trang phân quyền admin: loại trừ user bị ban và những
        /// volunteer chưa kích hoạt (IsEligibleRescuer = false).
        /// </summary>
        Task<PagedResult<UserModel>> GetPagedForPermissionAsync(int pageNumber, int pageSize, int? roleId = null, string? search = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy danh sách Id của tất cả admin đang hoạt động (không bị ban), dùng để gửi thông báo hệ thống.
        /// </summary>
        Task<List<Guid>> GetActiveAdminUserIdsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy danh sách Id của tất cả coordinator đang hoạt động (không bị ban), dùng để gửi thông báo hệ thống.
        /// </summary>
        Task<List<Guid>> GetActiveCoordinatorUserIdsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy danh sách Manager (RoleId=4, không bị ban). Nếu truyền excludeDepotId,
        /// loại trừ các manager đang active (UnassignedAt IS NULL) trong kho đó.
        /// Dùng cho dropdown chọn manager khi gán kho.
        /// </summary>
        Task<List<AvailableManagerDto>> GetAvailableManagersAsync(int? excludeDepotId = null, CancellationToken cancellationToken = default);
    }
}
