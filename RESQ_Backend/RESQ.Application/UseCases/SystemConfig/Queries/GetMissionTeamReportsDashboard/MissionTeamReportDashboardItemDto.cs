namespace RESQ.Application.UseCases.SystemConfig.Queries.GetMissionTeamReportsDashboard;

public class MissionTeamReportDashboardItemDto
{
    public int MissionId { get; set; }
    public int MissionTeamId { get; set; }
    public int TeamId { get; set; }
    public string? TeamCode { get; set; }
    public string? TeamName { get; set; }
    public int? AssemblyPointId { get; set; }
    public string? AssemblyPointName { get; set; }
    public string? MissionType { get; set; }
    public string? MissionStatus { get; set; }
    public string ExecutionStatus { get; set; } = string.Empty;
    public string ReportStatus { get; set; } = string.Empty;
    public DateTime? LastEditedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
}
