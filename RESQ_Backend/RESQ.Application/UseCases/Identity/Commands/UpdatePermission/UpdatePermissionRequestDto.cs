namespace RESQ.Application.UseCases.Identity.Commands.UpdatePermission;

public class UpdatePermissionRequestDto
{
    public string Code { get; set; } = null!;
    public string? Name { get; set; }
    public string? Description { get; set; }
}
