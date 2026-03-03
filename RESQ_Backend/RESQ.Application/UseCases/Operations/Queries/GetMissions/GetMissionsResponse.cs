namespace RESQ.Application.UseCases.Operations.Queries.GetMissions;

public class GetMissionsResponse
{
    public List<MissionDto> Missions { get; set; } = [];
}

public class MissionDto
{
    public int Id { get; set; }
    public int? ClusterId { get; set; }
    public string? MissionType { get; set; }
    public double? PriorityScore { get; set; }
    public string? Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? ExpectedEndTime { get; set; }
    public bool? IsCompleted { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ActivityCount { get; set; }
    public List<MissionActivityDto> Activities { get; set; } = [];
}

public class MissionActivityDto
{
    public int Id { get; set; }
    public int? Step { get; set; }
    public string? ActivityCode { get; set; }
    public string? ActivityType { get; set; }
    public string? Description { get; set; }
    public string? Target { get; set; }
    public string? Items { get; set; }
    public double? TargetLatitude { get; set; }
    public double? TargetLongitude { get; set; }
    public string? Status { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
