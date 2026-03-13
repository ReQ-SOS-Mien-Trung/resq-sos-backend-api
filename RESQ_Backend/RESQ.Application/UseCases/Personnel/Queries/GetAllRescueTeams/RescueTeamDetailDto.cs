namespace RESQ.Application.UseCases.Personnel.Queries.GetAllRescueTeams;

public class RescueTeamDetailDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TeamType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int AssemblyPointId { get; set; }
    public string? AssemblyPointName { get; set; }
    public string ManagedBy { get; set; } = string.Empty;
    public int MaxMembers { get; set; }
    public DateTime? AssemblyDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<RescueTeamMemberDto> Members { get; set; } = new();
}
