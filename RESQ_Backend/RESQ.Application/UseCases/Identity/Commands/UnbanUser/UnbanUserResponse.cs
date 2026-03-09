namespace RESQ.Application.UseCases.Identity.Commands.UnbanUser;

public class UnbanUserResponse
{
    public Guid UserId { get; set; }
    public bool IsBanned { get; set; }
}
