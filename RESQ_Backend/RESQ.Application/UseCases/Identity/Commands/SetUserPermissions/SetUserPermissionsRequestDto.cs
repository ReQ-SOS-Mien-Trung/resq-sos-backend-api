namespace RESQ.Application.UseCases.Identity.Commands.SetUserPermissions;

public class SetUserPermissionsRequestDto
{
    public List<int> PermissionIds { get; set; } = [];
}
