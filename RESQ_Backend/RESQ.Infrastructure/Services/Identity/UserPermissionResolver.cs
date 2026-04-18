using RESQ.Application.Repositories.Identity;
using RESQ.Application.Services;

namespace RESQ.Infrastructure.Services.Identity;

public sealed class UserPermissionResolver(
    IUserRepository userRepository,
    IPermissionRepository permissionRepository) : IUserPermissionResolver
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IPermissionRepository _permissionRepository = permissionRepository;

    public async Task<IReadOnlyCollection<string>> GetEffectivePermissionCodesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return [];

        var codes = await _permissionRepository
            .GetEffectivePermissionCodesAsync(userId, user.RoleId, cancellationToken);

        return codes;
    }
}
