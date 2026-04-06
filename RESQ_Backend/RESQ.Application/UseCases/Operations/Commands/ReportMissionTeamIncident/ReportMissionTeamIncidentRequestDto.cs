namespace RESQ.Application.UseCases.Operations.Commands.ReportMissionTeamIncident;

public class ReportMissionTeamIncidentRequestDto
{
    public string Description { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}