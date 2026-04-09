using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.CreateMission;

public record CreateMissionCommand(
    int ClusterId,
    string? MissionType,
    double? PriorityScore,
    DateTime? StartTime,
    DateTime? ExpectedEndTime,
    List<CreateActivityItemDto> Activities,
    Guid CreatedById
) : IRequest<CreateMissionResponse>;

public class CreateActivityItemDto
{
    public int? Step { get; set; }
    public string? ActivityType { get; set; }
    public string? Description { get; set; }
    public string? Priority { get; set; }
    public int? EstimatedTime { get; set; }
    public int? SosRequestId { get; set; }
    public int? DepotId { get; set; }
    public string? DepotName { get; set; }
    public string? DepotAddress { get; set; }
    public int? AssemblyPointId { get; set; }
    public List<SuggestedSupplyItemDto>? SuppliesToCollect { get; set; }
    public string? Target { get; set; }
    public double? TargetLatitude { get; set; }
    public double? TargetLongitude { get; set; }
    /// <summary>Rescue team ID from AI suggestion (suggestedTeam.id)</summary>
    public int? RescueTeamId { get; set; }
}

public class SuggestedSupplyItemDto
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public int? Quantity { get; set; }
    public string? Unit { get; set; }
    /// <summary>Tỉ lệ dự trù buffer (0.0–1.0). Nếu không truyền, hệ thống dùng giá trị mặc định 10%.</summary>
    public double? BufferRatio { get; set; }
}
