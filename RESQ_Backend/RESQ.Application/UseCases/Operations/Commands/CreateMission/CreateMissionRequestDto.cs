namespace RESQ.Application.UseCases.Operations.Commands.CreateMission;

public class CreateMissionRequestDto
{
    public int ClusterId { get; set; }
    public string? MissionType { get; set; }
    public double? PriorityScore { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? ExpectedEndTime { get; set; }
    /// <summary>
    /// List of activities to create. Each item maps directly to the AI mission
    /// suggestion response fields (activityType, priority, estimatedTime,
    /// sosRequestId, depotId, depotName, depotAddress, suppliesToCollect,
    /// suggestedTeam → rescueTeamId).
    /// </summary>
    public List<CreateActivityItemDto> Activities { get; set; } = [];
}
