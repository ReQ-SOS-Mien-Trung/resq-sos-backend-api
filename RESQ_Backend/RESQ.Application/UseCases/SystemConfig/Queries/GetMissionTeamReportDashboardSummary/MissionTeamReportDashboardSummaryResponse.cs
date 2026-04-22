namespace RESQ.Application.UseCases.SystemConfig.Queries.GetMissionTeamReportDashboardSummary;

public class MissionTeamReportDashboardSummaryResponse
{
    public int TotalCompletedTeams { get; set; }
    public int NotStartedCount { get; set; }
    public int DraftCount { get; set; }
    public int SubmittedCount { get; set; }
    public double SubmissionRate { get; set; }
}
