namespace RESQ.Application.UseCases.Operations.Queries.GetMissionTeamReport;

public class MissionTeamReportResponse
{
    public int MissionId { get; set; }
    public int MissionTeamId { get; set; }
    public string ExecutionStatus { get; set; } = string.Empty;
    public string ReportStatus { get; set; } = string.Empty;
    public bool CanEdit { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanEvaluateMembers { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? LastEditedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? TeamSummary { get; set; }
    public string? TeamNote { get; set; }
    public string? IssuesJson { get; set; }
    public string? ResultJson { get; set; }
    public string? EvidenceJson { get; set; }
    public List<MissionTeamReportActivityDto> Activities { get; set; } = [];
    public List<MissionTeamReportMemberEvaluationDto> MemberEvaluations { get; set; } = [];
}

public class MissionTeamReportActivityDto
{
    public int MissionActivityId { get; set; }
    public string? ActivityCode { get; set; }
    public string? ActivityType { get; set; }
    public string? ActivityStatus { get; set; }
    public string? ExecutionStatus { get; set; }
    public string? Summary { get; set; }
    public string? IssuesJson { get; set; }
    public string? ResultJson { get; set; }
    public string? EvidenceJson { get; set; }
}

public class MissionTeamReportMemberEvaluationDto
{
    public Guid RescuerId { get; set; }
    public string? FullName { get; set; }
    public string? Username { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? RescuerType { get; set; }
    public string? RoleInTeam { get; set; }
    public decimal? ResponseTimeScore { get; set; }
    public decimal? RescueEffectivenessScore { get; set; }
    public decimal? DecisionHandlingScore { get; set; }
    public decimal? SafetyMedicalSkillScore { get; set; }
    public decimal? TeamworkCommunicationScore { get; set; }
    public decimal? OverallScore { get; set; }
}
