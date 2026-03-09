namespace RESQ.Application.UseCases.Identity.Commands.AssignRoleToUser;

public class AssignRoleToUserResponse
{
    public Guid UserId { get; set; }
    public int RoleId { get; set; }
}
