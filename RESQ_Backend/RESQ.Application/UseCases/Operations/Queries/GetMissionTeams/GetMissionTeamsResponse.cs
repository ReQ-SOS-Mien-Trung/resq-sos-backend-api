namespace RESQ.Application.UseCases.Operations.Queries.GetMissionTeams;

public class GetMissionTeamsResponse
{
    public int MissionId { get; set; }
    public List<MissionTeamDto> Teams { get; set; } = [];
}

public class MissionTeamDto
{
    public int MissionTeamId { get; set; }
    public int RescueTeamId { get; set; }
    public string? TeamName { get; set; }
    public string? TeamCode { get; set; }
    public string? AssemblyPointName { get; set; }
    public string? TeamType { get; set; }
    public string? Status { get; set; }
    public string? Note { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? LocationUpdatedAt { get; set; }
    public string? LocationSource { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }
}
