namespace RESQ.Application.UseCases.Personnel.RescueTeams.DTOs;

public class AddMemberRequestDto
{
    public Guid UserId { get; set; }
    public int EventId { get; set; }
    public bool IsLeader { get; set; }
}
