using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionPendingActivities;

public class UpdateMissionPendingActivitiesRequestDto
{
    public List<UpdateMissionPendingActivityRequestItemDto> Activities { get; set; } = [];
}

public class UpdateMissionPendingActivityRequestItemDto
{
    public int ActivityId { get; set; }
    public int? Step { get; set; }
    public string? Description { get; set; }
    public string? Target { get; set; }
    public double? TargetLatitude { get; set; }
    public double? TargetLongitude { get; set; }
    public List<SupplyToCollectDto>? Items { get; set; }
}