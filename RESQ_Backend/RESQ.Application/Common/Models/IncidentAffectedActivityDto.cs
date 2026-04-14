namespace RESQ.Application.Common.Models;

public class IncidentAffectedActivityDto
{
    public int MissionActivityId { get; set; }
    public int OrderIndex { get; set; }
    public bool IsPrimary { get; set; }
    public int? Step { get; set; }
    public string? ActivityType { get; set; }
    public string? Status { get; set; }
}
