namespace RESQ.Application.UseCases.Identity.Commands.SetUserPermissions;

public class SetUserPermissionsResponse
{
    public Guid UserId { get; set; }
    public List<int> PermissionIds { get; set; } = [];
}
