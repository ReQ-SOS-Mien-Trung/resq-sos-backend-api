namespace RESQ.Application.UseCases.Identity.Commands.CreatePermission;

public class CreatePermissionRequestDto
{
    public string Code { get; set; } = null!;
    public string? Name { get; set; }
    public string? Description { get; set; }
}
