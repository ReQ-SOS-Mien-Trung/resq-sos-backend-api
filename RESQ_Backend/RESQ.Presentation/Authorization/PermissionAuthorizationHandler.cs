using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Presentation.Authorization;

/// <summary>
/// Xử lý <see cref="PermissionRequirement"/>.
/// Tra cứu quyền của user từ DB (role_permissions + user_permissions override),
/// cache trong bộ nhớ 5 phút để giảm tải DB.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;

    public PermissionAuthorizationHandler(IServiceScopeFactory scopeFactory, IMemoryCache cache)
    {
        _scopeFactory = scopeFactory;
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

    // ── Cache + DB lookup ─────────────────────────────────────────────────
    private async Task<HashSet<string>> GetUserPermissionsAsync(Guid userId)
    {
        var cacheKey = $"perms:{userId}";

        if (_cache.TryGetValue(cacheKey, out HashSet<string>? cached) && cached is not null)
            return cached;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResQDbContext>();

        // 1. Quyền từ role
        var roleCodes = await db.Users
            .Where(u => u.Id == userId)
            .SelectMany(u => u.Role!.RolePermissions)
            .Where(rp => rp.IsGranted == true)
            .Select(rp => rp.Claim!.Code!)
            .ToListAsync();

        var permissions = new HashSet<string>(roleCodes, StringComparer.OrdinalIgnoreCase);

        // 2. Override cấp user (grant thêm hoặc revoke)
        var userOverrides = await db.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => new { up.IsGranted, Code = up.Claim!.Code! })
            .ToListAsync();

        foreach (var override_ in userOverrides)
        {
            if (override_.IsGranted == true)
                permissions.Add(override_.Code);
            else if (override_.IsGranted == false)
                permissions.Remove(override_.Code);
        }

        _cache.Set(cacheKey, permissions, TimeSpan.FromMinutes(5));
        return permissions;
    }
}
