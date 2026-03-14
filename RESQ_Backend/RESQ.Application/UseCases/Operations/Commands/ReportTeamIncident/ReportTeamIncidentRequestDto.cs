namespace RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;

public class ReportTeamIncidentRequestDto
{
    public int MissionTeamId { get; set; }
    public string Description { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
