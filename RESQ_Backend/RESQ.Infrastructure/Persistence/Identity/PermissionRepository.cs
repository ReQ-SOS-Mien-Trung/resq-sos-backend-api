using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Persistence.Identity;

public class PermissionRepository(IUnitOfWork unitOfWork) : IPermissionRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<List<PermissionModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<Permission>().GetAllByPropertyAsync();
        return entities.Select(MapToModel).OrderBy(p => p.Id).ToList();
    }

    public async Task<PermissionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Permission>().GetByPropertyAsync(p => p.Id == id);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<PermissionModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Permission>().GetByPropertyAsync(p => p.Code == code);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<int> CreateAsync(PermissionModel model, CancellationToken cancellationToken = default)
    {
        var entity = new Permission
        {
            Code = model.Code,
            Name = model.Name,
            Description = model.Description
        };
        await _unitOfWork.GetRepository<Permission>().AddAsync(entity);
        await _unitOfWork.SaveAsync();
        return entity.Id;
    }

    public async Task UpdateAsync(PermissionModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Permission>().GetByPropertyAsync(p => p.Id == model.Id);
        if (entity is not null)
        {
            entity.Code = model.Code;
            entity.Name = model.Name;
            entity.Description = model.Description;
            await _unitOfWork.GetRepository<Permission>().UpdateAsync(entity);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.GetRepository<Permission>().DeleteAsyncById(id);
    }

    public async Task<List<PermissionModel>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userPermissions = await _unitOfWork.GetRepository<UserPermission>()
            .GetAllByPropertyAsync(up => up.UserId == userId && up.IsGranted == true, includeProperties: "Claim");
        return userPermissions.Select(up => new PermissionModel
        {
            Id = up.Claim.Id,
            Code = up.Claim.Code,
            Name = up.Claim.Name,
            Description = up.Claim.Description
        }).ToList();
    }

    public async Task SetUserPermissionsAsync(Guid userId, Guid grantedBy, List<int> permissionIds, CancellationToken cancellationToken = default)
    {
        // Remove all existing user permissions
        var existing = await _unitOfWork.GetRepository<UserPermission>()
            .GetAllByPropertyAsync(up => up.UserId == userId);
        foreach (var up in existing)
        {
            await _unitOfWork.GetRepository<UserPermission>().DeleteAsync(up.UserId, up.ClaimId);
        }

        // Add new ones
        var now = DateTime.UtcNow;
        var newEntries = permissionIds.Distinct().Select(pid => new UserPermission
        {
            UserId = userId,
            ClaimId = pid,
            IsGranted = true,
            GrantedBy = grantedBy,
            GrantedAt = now
        }).ToList();

        if (newEntries.Count > 0)
            await _unitOfWork.GetRepository<UserPermission>().AddRangeAsync(newEntries);
    }

    public async Task<List<string>> GetEffectivePermissionCodesAsync(Guid userId, int? roleId, CancellationToken cancellationToken = default)
    {
        // 1. Quyền từ role
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (roleId.HasValue)
        {
            var rolePerms = await _unitOfWork.GetRepository<RolePermission>()
                .GetAllByPropertyAsync(rp => rp.RoleId == roleId.Value && rp.IsGranted == true, includeProperties: "Claim");
            foreach (var rp in rolePerms)
                if (rp.Claim?.Code is not null)
                    permissions.Add(rp.Claim.Code);
        }

        // 2. Override cấp user (grant thêm hoặc revoke)
        var userOverrides = await _unitOfWork.GetRepository<UserPermission>()
            .GetAllByPropertyAsync(up => up.UserId == userId, includeProperties: "Claim");
        foreach (var up in userOverrides)
        {
            if (up.Claim?.Code is null) continue;
            if (up.IsGranted == true)  permissions.Add(up.Claim.Code);
            else if (up.IsGranted == false) permissions.Remove(up.Claim.Code);
        }

        return [..permissions];
    }

    private static PermissionModel MapToModel(Permission entity) => new()
    {
        Id = entity.Id,
        Code = entity.Code,
        Name = entity.Name,
        Description = entity.Description
    };
}
