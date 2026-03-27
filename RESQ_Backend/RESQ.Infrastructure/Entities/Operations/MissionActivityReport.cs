using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Operations;

[Table("mission_activity_reports")]
public partial class MissionActivityReport
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("mission_team_report_id")]
    public int MissionTeamReportId { get; set; }

    [Column("mission_activity_id")]
    public int MissionActivityId { get; set; }

    [Column("activity_code")]
    public string? ActivityCode { get; set; }

    [Column("activity_type")]
    public string? ActivityType { get; set; }

    [Column("execution_status")]
    public string? ExecutionStatus { get; set; }

    [Column("summary")]
    public string? Summary { get; set; }

    [Column("issues_json", TypeName = "jsonb")]
    public string? IssuesJson { get; set; }

    [Column("result_json", TypeName = "jsonb")]
    public string? ResultJson { get; set; }

    [Column("evidence_json", TypeName = "jsonb")]
    public string? EvidenceJson { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("MissionActivityId")]
    public virtual MissionActivity? MissionActivity { get; set; }

    [ForeignKey("MissionTeamReportId")]
    public virtual MissionTeamReport? MissionTeamReport { get; set; }
}