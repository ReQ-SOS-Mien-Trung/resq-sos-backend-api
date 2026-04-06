namespace RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;

public class ReportTeamIncidentResponse
{
    public int IncidentId { get; set; }
    public int MissionId { get; set; }
    public int MissionTeamId { get; set; }
    public int? MissionActivityId { get; set; }
    public string IncidentScope { get; set; } = "Mission";
    public string Status { get; set; } = "Reported";
    public DateTime ReportedAt { get; set; }
}
