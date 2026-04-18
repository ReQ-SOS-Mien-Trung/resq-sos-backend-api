using RESQ.Application.UseCases.Operations.Shared;

namespace RESQ.Application.UseCases.Operations.Commands.SubmitMissionTeamReport;

public class SubmitMissionTeamReportRequestDto
{
    public string? TeamSummary { get; set; }
    public string? TeamNote { get; set; }
    public string? IssuesJson { get; set; }
    public string? ResultJson { get; set; }
    public string? EvidenceJson { get; set; }
    public List<SubmitMissionTeamReportActivityItemDto> Activities { get; set; } = [];
    public List<MissionTeamMemberEvaluationInputDto> MemberEvaluations { get; set; } = [];
}

public class SubmitMissionTeamReportActivityItemDto
{
    public int MissionActivityId { get; set; }
    public string? ExecutionStatus { get; set; }
    public string? Summary { get; set; }
    public string? IssuesJson { get; set; }
    public string? ResultJson { get; set; }
    public string? EvidenceJson { get; set; }
}
