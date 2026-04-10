using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionPendingActivities;

public class UpdateMissionPendingActivitiesResponse
{
    public int MissionId { get; set; }
    public List<UpdatedMissionPendingActivityDto> Activities { get; set; } = [];
}

public class UpdatedMissionPendingActivityDto
{
    public int ActivityId { get; set; }
    public int? MissionId { get; set; }
    public int? MissionTeamId { get; set; }
    public int? Step { get; set; }
    public string? Description { get; set; }
    public string? Target { get; set; }
    public double? TargetLatitude { get; set; }
    public double? TargetLongitude { get; set; }
    public string? Status { get; set; }
    public List<SupplyToCollectDto>? SuppliesToCollect { get; set; }
}