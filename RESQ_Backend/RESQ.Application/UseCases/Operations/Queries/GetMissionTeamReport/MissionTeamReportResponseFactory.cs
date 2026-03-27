using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionTeamReport;

internal static class MissionTeamReportResponseFactory
{
    internal static MissionTeamReportResponse Create(
        int missionId,
        MissionTeamModel missionTeam,
        MissionTeamReportModel? report,
        IEnumerable<MissionActivityModel> activities,
        Guid requestedBy)
    {
        var isMember = missionTeam.RescueTeamMembers.Any(x => x.UserId == requestedBy);
        var isLeader = missionTeam.RescueTeamMembers.Any(x => x.UserId == requestedBy && x.IsLeader);
        var reportStatus = report?.ReportStatus.ToString() ?? MissionTeamReportStatus.NotStarted.ToString();
        var isSubmitted = string.Equals(reportStatus, MissionTeamReportStatus.Submitted.ToString(), StringComparison.OrdinalIgnoreCase);
        var isCancelled = string.Equals(missionTeam.Status, MissionTeamExecutionStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase);

        return new MissionTeamReportResponse
        {
            MissionId = missionId,
            MissionTeamId = missionTeam.Id,
            ExecutionStatus = missionTeam.Status ?? MissionTeamExecutionStatus.Assigned.ToString(),
            ReportStatus = reportStatus,
            CanEdit = isMember && !isCancelled && !isSubmitted,
            CanSubmit = isLeader
                && !isCancelled
                && !isSubmitted
                && string.Equals(missionTeam.Status, MissionTeamExecutionStatus.CompletedWaitingReport.ToString(), StringComparison.OrdinalIgnoreCase),
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
                        ActivityCode = activity.ActivityCode,
                        ActivityType = activity.ActivityType,
                        ActivityStatus = activity.Status.ToString(),
                        ExecutionStatus = activityReport?.ExecutionStatus,
                        Summary = activityReport?.Summary,
                        IssuesJson = activityReport?.IssuesJson,
                        ResultJson = activityReport?.ResultJson,
                        EvidenceJson = activityReport?.EvidenceJson
                    };
                })
                .ToList()
        };
    }
}