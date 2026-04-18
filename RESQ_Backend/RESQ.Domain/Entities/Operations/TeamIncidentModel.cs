using RESQ.Domain.Enum.Operations;

namespace RESQ.Domain.Entities.Operations;

public class TeamIncidentModel
{
    public int Id { get; set; }
    public int MissionTeamId { get; set; }
    public int? MissionActivityId { get; set; }
    public TeamIncidentScope IncidentScope { get; set; } = TeamIncidentScope.Mission;
    public string? IncidentType { get; set; }
    public string? DecisionCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Description { get; set; }
    public string? DetailJson { get; set; }
    public int PayloadVersion { get; set; } = 1;
    public bool NeedSupportSos { get; set; }
    public bool NeedReassignActivity { get; set; }
    public int? SupportSosRequestId { get; set; }
    public TeamIncidentStatus Status { get; set; }
    public Guid? ReportedBy { get; set; }
    public DateTime? ReportedAt { get; set; }
    public List<TeamIncidentAffectedActivityModel> AffectedActivities { get; set; } = [];
}

public class TeamIncidentAffectedActivityModel
{
    public int MissionActivityId { get; set; }
    public int OrderIndex { get; set; }
    public bool IsPrimary { get; set; }
    public int? Step { get; set; }
    public string? ActivityType { get; set; }
    public MissionActivityStatus? Status { get; set; }
}
