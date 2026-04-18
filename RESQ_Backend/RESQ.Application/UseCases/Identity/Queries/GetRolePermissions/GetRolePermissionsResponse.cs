namespace RESQ.Application.UseCases.Identity.Queries.GetRolePermissions;

public class PermissionDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public class GetRolePermissionsResponse
{
    public int RoleId { get; set; }
    public List<PermissionDto> Permissions { get; set; } = [];
}
