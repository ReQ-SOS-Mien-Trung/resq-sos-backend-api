namespace RESQ.Domain.Entities.Operations;

public class MissionActivityReportModel
{
    public int Id { get; set; }
    public int MissionTeamReportId { get; set; }
    public int MissionActivityId { get; set; }
    public string? ActivityCode { get; set; }
    public string? ActivityType { get; set; }
    public string? ExecutionStatus { get; set; }
    public string? Summary { get; set; }
    public string? IssuesJson { get; set; }
    public string? ResultJson { get; set; }
    public string? EvidenceJson { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}