using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.Repositories.Identity;

public interface IPermissionRepository
{
    Task<List<PermissionModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PermissionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PermissionModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(PermissionModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(PermissionModel model, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<List<PermissionModel>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SetUserPermissionsAsync(Guid userId, Guid grantedBy, List<int> permissionIds, CancellationToken cancellationToken = default);
    Task<List<string>> GetEffectivePermissionCodesAsync(Guid userId, int? roleId, CancellationToken cancellationToken = default);
}
