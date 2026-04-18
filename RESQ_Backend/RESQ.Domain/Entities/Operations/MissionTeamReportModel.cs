using RESQ.Domain.Enum.Operations;

namespace RESQ.Domain.Entities.Operations;

public class MissionTeamReportModel
{
    public int Id { get; set; }
    public int MissionTeamId { get; set; }
    public MissionTeamReportStatus ReportStatus { get; set; } = MissionTeamReportStatus.NotStarted;
    public string? TeamSummary { get; set; }
    public string? TeamNote { get; set; }
    public string? IssuesJson { get; set; }
    public string? ResultJson { get; set; }
    public string? EvidenceJson { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? LastEditedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedBy { get; set; }
    public List<MissionActivityReportModel> ActivityReports { get; set; } = [];
    public List<MissionTeamMemberEvaluationModel> MemberEvaluations { get; set; } = [];
}
