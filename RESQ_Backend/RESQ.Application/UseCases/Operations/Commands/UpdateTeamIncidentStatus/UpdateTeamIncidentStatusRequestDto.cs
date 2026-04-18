namespace RESQ.Application.UseCases.Operations.Commands.UpdateTeamIncidentStatus;

public class UpdateTeamIncidentStatusRequestDto
{
    /// <summary>Target status: InProgress, Resolved</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>When resolving manually: true → team becomes Unavailable, false → Available</summary>
    public bool? HasInjuredMember { get; set; }
}
