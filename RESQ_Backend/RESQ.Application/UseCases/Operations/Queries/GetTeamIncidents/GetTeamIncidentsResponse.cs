using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Operations.Queries.GetTeamIncidents;

public class GetTeamIncidentsResponse
{
    public int MissionId { get; set; }
    public List<TeamIncidentDto> Incidents { get; set; } = [];
}

public class TeamIncidentDto
{
    public int IncidentId { get; set; }
    public int MissionTeamId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public ReportedByDto? ReportedBy { get; set; }
    public DateTime? ReportedAt { get; set; }
}
