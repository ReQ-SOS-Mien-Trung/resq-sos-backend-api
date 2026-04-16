namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamDetail;

public class AdminTeamDetailDto
{
    // ── Team info ─────────────────────────────────────────────────────────
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TeamType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? AssemblyPointId { get; set; }
    public string? AssemblyPointName { get; set; }
    public string? ManagedByName { get; set; }
    public int MaxMembers { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ── Members ────────────────────────────────────────────────────────────
    public List<AdminTeamMemberDto> Members { get; set; } = [];

    // ── Missions (past + current) ──────────────────────────────────────────
    public List<AdminTeamMissionDto> Missions { get; set; } = [];

    // ── Mission completion rate (pie chart) ────────────────────────────────
    public MissionCompletionRateDto CompletionRate { get; set; } = new();
}

public class AdminTeamMemberDto
{
    public Guid UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public string? RescuerType { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsLeader { get; set; }
    public string? RoleInTeam { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class AdminTeamMissionDto
{
    public int MissionTeamId { get; set; }
    public int MissionId { get; set; }
    public string MissionStatus { get; set; } = string.Empty;
    public string? MissionType { get; set; }
    public string TeamAssignmentStatus { get; set; } = string.Empty;
    public DateTime? AssignedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }
    public DateTime? MissionStartTime { get; set; }
    public DateTime? MissionCompletedAt { get; set; }
    public bool? IsCompleted { get; set; }
    public string? ReportStatus { get; set; }
    public List<AdminMissionActivityDto> Activities { get; set; } = [];
}

public class AdminMissionActivityDto
{
    public int Id { get; set; }
    public int? Step { get; set; }
    public string? ActivityType { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class MissionCompletionRateDto
{
    public int TotalMissions { get; set; }
    public int CompletedCount { get; set; }
    public int IncompletedCount { get; set; }
    public double CompletedPercent { get; set; }
    public double IncompletedPercent { get; set; }
}
