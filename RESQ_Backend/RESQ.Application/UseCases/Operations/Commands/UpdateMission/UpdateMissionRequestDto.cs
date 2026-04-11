using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMission;

public class UpdateMissionRequestDto
{
    public string? MissionType { get; set; }
    public double? PriorityScore { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? ExpectedEndTime { get; set; }
    public List<UpdateMissionActivityRequestItemDto> Activities { get; set; } = [];
}

public class UpdateMissionActivityRequestItemDto
{
    public int ActivityId { get; set; }
    public int? Step { get; set; }
    public string? Description { get; set; }
    public string? Target { get; set; }
    public double? TargetLatitude { get; set; }
    public double? TargetLongitude { get; set; }
    public List<SupplyToCollectDto>? Items { get; set; }
}
