namespace RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById;

public class AssemblyPointTeamMemberDto
{
    public Guid UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? RoleInTeam { get; set; }
    public bool IsLeader { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class AssemblyPointTeamDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? TeamType { get; set; }
    public string? Status { get; set; }
    public int? MaxMembers { get; set; }
    public List<AssemblyPointTeamMemberDto> Members { get; set; } = [];
}

public class AssemblyPointDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int CapacityTeams { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastUpdatedAt { get; set; }
    public List<AssemblyPointTeamDto> Teams { get; set; } = [];
}
