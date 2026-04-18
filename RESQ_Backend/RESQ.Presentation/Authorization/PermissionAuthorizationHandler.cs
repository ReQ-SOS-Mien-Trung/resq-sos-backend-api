using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using RESQ.Application.Services;

namespace RESQ.Presentation.Authorization;

/// <summary>
/// Xử lý <see cref="PermissionRequirement"/>.
/// Tra cứu quyền của user từ DB (role_permissions + user_permissions override),
/// cache trong bộ nhớ 5 phút để giảm tải DB.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IUserPermissionResolver _permissionResolver;
    private readonly IMemoryCache _cache;

    public PermissionAuthorizationHandler(IUserPermissionResolver permissionResolver, IMemoryCache cache)
    {
        _permissionResolver = permissionResolver;
        _cache = cache;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userIdStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return;

        var permissions = await GetUserPermissionsAsync(userId);

        if (requirement.PermissionCodes.Any(code =>
                permissions.Contains(code, StringComparer.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
        }
    }

    // -- Cache + DB lookup -------------------------------------------------
    private async Task<HashSet<string>> GetUserPermissionsAsync(Guid userId)
    {
        var cacheKey = $"perms:{userId}";

        if (_cache.TryGetValue(cacheKey, out HashSet<string>? cached) && cached is not null)
            return cached;

        var permissionCodes = await _permissionResolver.GetEffectivePermissionCodesAsync(userId);
        var permissions = new HashSet<string>(permissionCodes, StringComparer.OrdinalIgnoreCase);

        _cache.Set(cacheKey, permissions, TimeSpan.FromMinutes(5));
        return permissions;
    }
}
