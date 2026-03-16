namespace RESQ.Domain.Entities.Operations;

public class MissionTeamModel
{
    public int Id { get; set; }
    public int MissionId { get; set; }
    public int RescuerTeamId { get; set; }
    public string? TeamType { get; set; }
    public string? Status { get; set; }
    public string? Note { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? LocationUpdatedAt { get; set; }
    public string? LocationSource { get; set; }

    // Display hydration
    public string? TeamName { get; set; }
    public string? TeamCode { get; set; }
    public string? AssemblyPointName { get; set; }
}
