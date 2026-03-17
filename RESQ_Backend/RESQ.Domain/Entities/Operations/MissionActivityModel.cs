using RESQ.Domain.Enum.Operations;

namespace RESQ.Domain.Entities.Operations;

public class MissionActivityModel
{
    public int Id { get; set; }
    public int? MissionId { get; set; }
    public int? Step { get; set; }
    public string? ActivityCode { get; set; }
    public string? ActivityType { get; set; }
    public string? Description { get; set; }
    public string? Target { get; set; }
    public string? Items { get; set; }
    public double? TargetLatitude { get; set; }
    public double? TargetLongitude { get; set; }
    public MissionActivityStatus Status { get; set; } = MissionActivityStatus.Planned;
    public int? MissionTeamId { get; set; }
    public string? Priority { get; set; }
    public int? EstimatedTime { get; set; }
    public int? SosRequestId { get; set; }
    public int? DepotId { get; set; }
    public string? DepotName { get; set; }
    public string? DepotAddress { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? LastDecisionBy { get; set; }
}
