namespace RESQ.Application.UseCases.Identity.Commands.CreatePermission;

public class CreatePermissionResponse
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}
