using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.Repositories.Identity;

public interface IRoleRepository
{
    Task<List<RoleModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<RoleModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<RoleModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(RoleModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(RoleModel model, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<List<PermissionModel>> GetPermissionsAsync(int roleId, CancellationToken cancellationToken = default);
    Task SetPermissionsAsync(int roleId, List<int> permissionIds, CancellationToken cancellationToken = default);
}
