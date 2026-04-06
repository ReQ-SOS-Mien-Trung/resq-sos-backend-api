namespace RESQ.Application.UseCases.Operations.Queries.GetAllTeamIncidents;

public class GetAllTeamIncidentsResponse
{
    public List<TeamIncidentDto> Incidents { get; set; } = [];
}

public class TeamIncidentDto
{
    public int IncidentId { get; set; }
    public int MissionTeamId { get; set; }
    public int? MissionActivityId { get; set; }
    public string IncidentScope { get; set; } = "Mission";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public Guid? ReportedBy { get; set; }
    public DateTime? ReportedAt { get; set; }
}
