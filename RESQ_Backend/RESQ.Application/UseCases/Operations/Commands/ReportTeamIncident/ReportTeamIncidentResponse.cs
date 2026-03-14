namespace RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;

public class ReportTeamIncidentResponse
{
    public int IncidentId { get; set; }
    public int MissionTeamId { get; set; }
    public string Status { get; set; } = "Reported";
    public DateTime ReportedAt { get; set; }
}
