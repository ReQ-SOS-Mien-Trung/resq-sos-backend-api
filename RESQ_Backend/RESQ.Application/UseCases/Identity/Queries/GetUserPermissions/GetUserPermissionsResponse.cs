namespace RESQ.Application.UseCases.Identity.Queries.GetUserPermissions;

public class UserPermissionDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public class GetUserPermissionsResponse
{
    public Guid UserId { get; set; }
    public int? RoleId { get; set; }
    public List<UserPermissionDto> Permissions { get; set; } = [];
    public List<UserPermissionDto> RolePermissions { get; set; } = [];
}
