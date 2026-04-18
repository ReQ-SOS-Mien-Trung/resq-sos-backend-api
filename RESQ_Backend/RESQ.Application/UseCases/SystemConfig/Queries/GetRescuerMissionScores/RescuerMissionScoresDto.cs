namespace RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerMissionScores;

public class RescuerMissionScoresDto
{
    public Guid RescuerId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AvatarUrl { get; set; }

    // ── Overall aggregate score ────────────────────────────────────────────
    public OverallScoreDto? OverallScore { get; set; }

    // ── Per-mission evaluation history ────────────────────────────────────
    public List<MissionEvaluationDto> MissionEvaluations { get; set; } = [];

    // ── Team membership history ────────────────────────────────────────────
    public List<TeamMembershipHistoryDto> TeamHistory { get; set; } = [];
}

public class OverallScoreDto
{
    public decimal OverallAverageScore { get; set; }
    public int EvaluationCount { get; set; }

    // per-criteria averages (progress chart)
    public decimal ResponseTimeScore { get; set; }
    public decimal RescueEffectivenessScore { get; set; }
    public decimal DecisionHandlingScore { get; set; }
    public decimal SafetyMedicalSkillScore { get; set; }
    public decimal TeamworkCommunicationScore { get; set; }
}

public class MissionEvaluationDto
{
    public int EvaluationId { get; set; }
    public int MissionTeamReportId { get; set; }
    public int MissionId { get; set; }
    public string? MissionType { get; set; }
    public DateTime? MissionCompletedAt { get; set; }

    // Scores for this mission
    public decimal ResponseTimeScore { get; set; }
    public decimal RescueEffectivenessScore { get; set; }
    public decimal DecisionHandlingScore { get; set; }
    public decimal SafetyMedicalSkillScore { get; set; }
    public decimal TeamworkCommunicationScore { get; set; }
    public decimal AverageScore { get; set; }

    public DateTime? EvaluatedAt { get; set; }
}

public class TeamMembershipHistoryDto
{
    public int TeamId { get; set; }
    public string? TeamCode { get; set; }
    public string? TeamName { get; set; }
    public string? TeamType { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsLeader { get; set; }
    public string? RoleInTeam { get; set; }
    /// <summary>Thời gian chấp nhận vào đội (InvitedAt / RespondedAt).</summary>
    public DateTime JoinedAt { get; set; }
    /// <summary>Thời gian rời khỏi đội (null nếu vẫn còn trong đội).</summary>
    public DateTime? LeftAt { get; set; }
}
