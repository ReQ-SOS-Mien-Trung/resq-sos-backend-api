namespace RESQ.Application.UseCases.Identity.Commands.SetRolePermissions;

public class SetRolePermissionsResponse
{
    public int RoleId { get; set; }
    public List<int> PermissionIds { get; set; } = [];
}
