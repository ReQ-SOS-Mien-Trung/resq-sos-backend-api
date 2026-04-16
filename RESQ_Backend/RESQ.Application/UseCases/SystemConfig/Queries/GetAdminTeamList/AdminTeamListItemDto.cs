namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamList;

public class AdminTeamListItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TeamType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? AssemblyPointId { get; set; }
    public string? AssemblyPointName { get; set; }
    public int MaxMembers { get; set; }
    public int CurrentMemberCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
