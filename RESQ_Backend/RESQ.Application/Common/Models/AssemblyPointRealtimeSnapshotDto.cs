namespace RESQ.Application.Common.Models;

public sealed class AssemblyPointRealtimeMissionActivityDto
{
    public int Id { get; set; }
    public int? MissionId { get; set; }
    public int? Step { get; set; }
    public string? ActivityType { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? MissionTeamId { get; set; }
    public int? SosRequestId { get; set; }
    public int? AssemblyPointId { get; set; }
    public string? AssemblyPointName { get; set; }
}

public sealed class AssemblyPointRealtimeSnapshotDto
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = "AssemblyPointUpdated";
    public int AssemblyPointId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasActiveEvent { get; set; }
    public int ActiveMissionActivityCount { get; set; }
    public bool RequiresReroute { get; set; }
    public string? AlertMessage { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string Operation { get; set; } = string.Empty;
    public List<AssemblyPointRealtimeMissionActivityDto> ActiveMissionActivities { get; set; } = [];
}