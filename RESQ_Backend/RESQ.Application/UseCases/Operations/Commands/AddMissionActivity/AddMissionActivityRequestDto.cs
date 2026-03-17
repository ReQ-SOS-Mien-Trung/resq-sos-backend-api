using RESQ.Application.UseCases.Operations.Commands.CreateMission;

namespace RESQ.Application.UseCases.Operations.Commands.AddMissionActivity;

public class AddMissionActivityRequestDto
{
    public int? Step { get; set; }
    public string? ActivityCode { get; set; }
    public string? ActivityType { get; set; }
    public string? Description { get; set; }
    public string? Priority { get; set; }
    public int? EstimatedTime { get; set; }
    public int? SosRequestId { get; set; }
    public int? DepotId { get; set; }
    public string? DepotName { get; set; }
    public string? DepotAddress { get; set; }
    public List<SuggestedSupplyItemDto>? SuppliesToCollect { get; set; }
    public string? Target { get; set; }
    public double? TargetLatitude { get; set; }
    public double? TargetLongitude { get; set; }
    /// <summary>Rescue team ID (from AI suggestedTeam.id)</summary>
    public int? RescueTeamId { get; set; }
}
