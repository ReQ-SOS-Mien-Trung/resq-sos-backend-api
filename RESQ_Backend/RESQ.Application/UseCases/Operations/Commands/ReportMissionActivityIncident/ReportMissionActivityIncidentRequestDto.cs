namespace RESQ.Application.UseCases.Operations.Commands.ReportMissionActivityIncident;

public class ReportMissionActivityIncidentRequestDto
{
    public string Description { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}