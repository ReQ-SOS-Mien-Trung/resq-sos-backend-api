namespace RESQ.Application.UseCases.Identity.Commands.SetRolePermissions;

public class SetRolePermissionsRequestDto
{
    public List<int> PermissionIds { get; set; } = [];
}
