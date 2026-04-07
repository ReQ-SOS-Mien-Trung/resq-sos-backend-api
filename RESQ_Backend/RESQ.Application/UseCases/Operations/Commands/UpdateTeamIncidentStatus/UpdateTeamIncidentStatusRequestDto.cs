namespace RESQ.Application.UseCases.Operations.Commands.UpdateTeamIncidentStatus;

public class UpdateTeamIncidentStatusRequestDto
{
    /// <summary>Target status: InProgress, Resolved, Closed</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Optional when transitioning from Reported: true → InProgress, false → Closed</summary>
    public bool? NeedsAssistance { get; set; }
    /// <summary>When resolving: true → team becomes Unavailable, false → Available</summary>
    public bool? HasInjuredMember { get; set; }
}
