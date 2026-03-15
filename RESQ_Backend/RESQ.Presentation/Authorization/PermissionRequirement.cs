using Microsoft.AspNetCore.Authorization;

namespace RESQ.Presentation.Authorization;

/// <summary>
/// Biểu diễn yêu cầu quyền. Thỏa mãn khi user có ÍT NHẤT MỘT trong các permission codes chỉ định.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string[] PermissionCodes { get; }

    public PermissionRequirement(params string[] permissionCodes)
    {
        PermissionCodes = permissionCodes;
    }
}
