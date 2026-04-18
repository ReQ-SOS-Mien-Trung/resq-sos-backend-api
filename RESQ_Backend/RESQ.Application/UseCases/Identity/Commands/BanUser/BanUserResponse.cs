namespace RESQ.Application.UseCases.Identity.Commands.BanUser;

public class BanUserResponse
{
    public Guid UserId { get; set; }
    public bool IsBanned { get; set; }
    public Guid? BannedBy { get; set; }
    public DateTime? BannedAt { get; set; }
    public string? BanReason { get; set; }
}
