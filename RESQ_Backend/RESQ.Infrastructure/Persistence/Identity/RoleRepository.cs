using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Persistence.Identity;

public class RoleRepository(IUnitOfWork unitOfWork) : IRoleRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<List<RoleModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<Role>().GetAllByPropertyAsync();
        return entities.Select(MapToModel).OrderBy(r => r.Id).ToList();
    }

    public async Task<RoleModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Role>().GetByPropertyAsync(r => r.Id == id);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<RoleModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Role>().GetByPropertyAsync(r => r.Name == name);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<int> CreateAsync(RoleModel model, CancellationToken cancellationToken = default)
    {
        var entity = new Role { Name = model.Name };
        await _unitOfWork.GetRepository<Role>().AddAsync(entity);
        await _unitOfWork.SaveAsync();
        return entity.Id;
    }

    public async Task UpdateAsync(RoleModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Role>().GetByPropertyAsync(r => r.Id == model.Id);
        if (entity is not null)
        {
            entity.Name = model.Name;
            await _unitOfWork.GetRepository<Role>().UpdateAsync(entity);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.GetRepository<Role>().DeleteAsyncById(id);
    }

    public async Task<List<PermissionModel>> GetPermissionsAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var rolePermissions = await _unitOfWork.GetRepository<RolePermission>()
            .GetAllByPropertyAsync(rp => rp.RoleId == roleId && rp.IsGranted == true, includeProperties: "Claim");
        return rolePermissions.Select(rp => new PermissionModel
        {
            Id = rp.Claim.Id,
            Code = rp.Claim.Code,
            Name = rp.Claim.Name,
            Description = rp.Claim.Description
        }).ToList();
    }

    public async Task SetPermissionsAsync(int roleId, List<int> permissionIds, CancellationToken cancellationToken = default)
    {
        // Remove all existing role permissions
        var existing = await _unitOfWork.GetRepository<RolePermission>()
            .GetAllByPropertyAsync(rp => rp.RoleId == roleId);
        foreach (var rp in existing)
        {
            await _unitOfWork.GetRepository<RolePermission>().DeleteAsync(rp.RoleId, rp.ClaimId);
        }

        // Add new ones
        var newEntries = permissionIds.Distinct().Select(pid => new RolePermission
        {
            RoleId = roleId,
            ClaimId = pid,
            IsGranted = true
        }).ToList();

        if (newEntries.Count > 0)
            await _unitOfWork.GetRepository<RolePermission>().AddRangeAsync(newEntries);
    }

    private static RoleModel MapToModel(Role entity) => new() { Id = entity.Id, Name = entity.Name };
}
