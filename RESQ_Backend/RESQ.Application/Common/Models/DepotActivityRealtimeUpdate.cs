namespace RESQ.Application.Common.Models;

public sealed class DepotActivityRealtimeUpdate
{
    public int ActivityId { get; set; }
    public int DepotId { get; set; }
    public int? MissionId { get; set; }
    public int? MissionTeamId { get; set; }
    public int? RescueTeamId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? EstimatedTime { get; set; }
    public DateTime? MissionExpectedEndTime { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
