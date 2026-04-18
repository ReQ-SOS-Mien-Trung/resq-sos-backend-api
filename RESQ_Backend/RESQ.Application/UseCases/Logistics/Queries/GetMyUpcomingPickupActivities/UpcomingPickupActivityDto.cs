namespace RESQ.Application.UseCases.Logistics.Queries.GetMyUpcomingPickupActivities;

public class UpcomingPickupActivityDto
{
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public int MissionId { get; set; }
    public string? MissionType { get; set; }
    public string? MissionStatus { get; set; }
    public DateTime? MissionStartTime { get; set; }
    public DateTime? MissionExpectedEndTime { get; set; }
    public int ActivityId { get; set; }
    public int? Step { get; set; }
    public string? ActivityType { get; set; }
    public string? Description { get; set; }
    public string? Priority { get; set; }
    public int? EstimatedTime { get; set; }
    public string? Status { get; set; }
    public DateTime? AssignedAt { get; set; }
    public int? MissionTeamId { get; set; }
    public int? RescueTeamId { get; set; }
    public string? RescueTeamName { get; set; }
    public string? TeamType { get; set; }
    public List<UpcomingPickupItemDto> Items { get; set; } = [];
}

public class UpcomingPickupItemDto
{
    public int? ItemId { get; set; }
    public string? ItemName { get; set; }
    public string? ImageUrl { get; set; }
    public int Quantity { get; set; }
    public string? Unit { get; set; }
}
