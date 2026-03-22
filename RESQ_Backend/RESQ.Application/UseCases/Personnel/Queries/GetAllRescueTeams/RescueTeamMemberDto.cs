namespace RESQ.Application.UseCases.Personnel.Queries.GetAllRescueTeams;

public class RescueTeamMemberDto
{
    public Guid UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? RescuerType { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsLeader { get; set; }
    public string? RoleInTeam { get; set; }
    public DateTime JoinedAt { get; set; }
}
