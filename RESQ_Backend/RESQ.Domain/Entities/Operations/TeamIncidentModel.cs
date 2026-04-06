using RESQ.Domain.Enum.Operations;

namespace RESQ.Domain.Entities.Operations;

public class TeamIncidentModel
{
    public int Id { get; set; }
    public int MissionTeamId { get; set; }
    public int? MissionActivityId { get; set; }
    public TeamIncidentScope IncidentScope { get; set; } = TeamIncidentScope.Mission;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Description { get; set; }
    public TeamIncidentStatus Status { get; set; }
    public Guid? ReportedBy { get; set; }
    public DateTime? ReportedAt { get; set; }
}
