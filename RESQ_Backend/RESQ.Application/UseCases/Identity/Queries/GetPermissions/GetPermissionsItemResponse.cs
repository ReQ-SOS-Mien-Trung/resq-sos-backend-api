namespace RESQ.Application.UseCases.Identity.Queries.GetPermissions;

public class GetPermissionsItemResponse
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}
