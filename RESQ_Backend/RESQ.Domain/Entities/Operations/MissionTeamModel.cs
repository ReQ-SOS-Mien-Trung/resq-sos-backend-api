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
    public string? ReportStatus { get; set; }
    public DateTime? ReportStartedAt { get; set; }
    public DateTime? ReportLastEditedAt { get; set; }
    public DateTime? ReportSubmittedAt { get; set; }

    // Display hydration
    public string? TeamName { get; set; }
    public string? TeamCode { get; set; }
    public string? AssemblyPointName { get; set; }

    // Rescue team detail
    public string? TeamStatus { get; set; }
    public int? MaxMembers { get; set; }
    public int MemberCount { get; set; }
    public DateTime? AssemblyDate { get; set; }
    public List<MissionTeamMemberInfo> RescueTeamMembers { get; set; } = [];
}

public class MissionTeamMemberInfo
{
    public Guid UserId { get; set; }
    public string? FullName { get; set; }
    public string? Username { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? RescuerType { get; set; }
    public string? RoleInTeam { get; set; }
    public bool IsLeader { get; set; }
    public string? Status { get; set; }
    public bool CheckedIn { get; set; }
}
