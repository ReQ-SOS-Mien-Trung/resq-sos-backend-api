using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Application.UseCases.Operations.Shared;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionTeamReport;

internal static class MissionTeamReportResponseFactory
{
    internal static MissionTeamReportResponse Create(
        int missionId,
        MissionTeamModel missionTeam,
        MissionTeamReportModel? report,
        IEnumerable<MissionActivityModel> activities,
        Guid requestedBy,
        bool isReadOnlyViewer = false)
    {
        var isMember = missionTeam.RescueTeamMembers.Any(x => x.UserId == requestedBy);
        var isLeader = missionTeam.RescueTeamMembers.Any(x => x.UserId == requestedBy && x.IsLeader);
        var reportStatus = report?.ReportStatus.ToString() ?? MissionTeamReportStatus.NotStarted.ToString();
        var isSubmitted = string.Equals(reportStatus, MissionTeamReportStatus.Submitted.ToString(), StringComparison.OrdinalIgnoreCase);
        var isCancelled = string.Equals(missionTeam.Status, MissionTeamExecutionStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase);
        var evaluableMembers = MissionTeamMemberEvaluationHelper.GetEvaluableMembers(missionTeam);

        return new MissionTeamReportResponse
        {
            MissionId = missionId,
            MissionTeamId = missionTeam.Id,
            ExecutionStatus = missionTeam.Status ?? MissionTeamExecutionStatus.Assigned.ToString(),
            ReportStatus = reportStatus,
            CanEdit = !isReadOnlyViewer && isMember && !isCancelled && !isSubmitted,
            CanSubmit = !isReadOnlyViewer
                && isLeader
                && !isCancelled
                && !isSubmitted
                && string.Equals(missionTeam.Status, MissionTeamExecutionStatus.CompletedWaitingReport.ToString(), StringComparison.OrdinalIgnoreCase),
            CanEvaluateMembers = !isReadOnlyViewer && isLeader && !isCancelled && !isSubmitted,
            StartedAt = report?.StartedAt,
            LastEditedAt = report?.LastEditedAt,
            SubmittedAt = report?.SubmittedAt,
            TeamSummary = report?.TeamSummary,
            TeamNote = report?.TeamNote,
            IssuesJson = report?.IssuesJson,
            ResultJson = report?.ResultJson,
            EvidenceJson = report?.EvidenceJson,
            Activities = activities
                .OrderBy(x => x.Step)
                .ThenBy(x => x.Id)
                .Select(activity =>
                {
                    var activityReport = report?.ActivityReports.FirstOrDefault(x => x.MissionActivityId == activity.Id);
                    return new MissionTeamReportActivityDto
                    {
                        MissionActivityId = activity.Id,
                        ActivityType = activity.ActivityType,
                        ActivityStatus = activity.Status.ToString(),
                        ExecutionStatus = activityReport?.ExecutionStatus,
                        Summary = activityReport?.Summary,
                        IssuesJson = activityReport?.IssuesJson,
                        ResultJson = activityReport?.ResultJson,
                        EvidenceJson = activityReport?.EvidenceJson
                    };
                })
                .ToList(),
            MemberEvaluations = evaluableMembers.Values
                .OrderBy(member => member.FullName)
                .Select(member =>
                {
                    var evaluation = report?.MemberEvaluations.FirstOrDefault(x => x.RescuerId == member.UserId);
                    return new MissionTeamReportMemberEvaluationDto
                    {
                        RescuerId = member.UserId,
                        FullName = member.FullName,
                        Username = member.Username,
                        Phone = member.Phone,
                        AvatarUrl = member.AvatarUrl,
                        RescuerType = member.RescuerType,
                        RoleInTeam = member.RoleInTeam,
                        ResponseTimeScore = evaluation?.ResponseTimeScore,
                        RescueEffectivenessScore = evaluation?.RescueEffectivenessScore,
                        DecisionHandlingScore = evaluation?.DecisionHandlingScore,
                        SafetyMedicalSkillScore = evaluation?.SafetyMedicalSkillScore,
                        TeamworkCommunicationScore = evaluation?.TeamworkCommunicationScore,
                        OverallScore = evaluation?.OverallScore
                    };
                })
                .ToList()
        };
    }
}
