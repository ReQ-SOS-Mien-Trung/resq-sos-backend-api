using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Entities.Operations;

[Table("mission_team_reports")]
public partial class MissionTeamReport
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("mission_team_id")]
    public int MissionTeamId { get; set; }

    [Column("report_status")]
    [StringLength(50)]
    public string? ReportStatus { get; set; }

    [Column("team_summary")]
    public string? TeamSummary { get; set; }

    [Column("team_note")]
    public string? TeamNote { get; set; }

    [Column("issues_json", TypeName = "jsonb")]
    public string? IssuesJson { get; set; }

    [Column("result_json", TypeName = "jsonb")]
    public string? ResultJson { get; set; }

    [Column("evidence_json", TypeName = "jsonb")]
    public string? EvidenceJson { get; set; }

    [Column("started_at", TypeName = "timestamp with time zone")]
    public DateTime? StartedAt { get; set; }

    [Column("last_edited_at", TypeName = "timestamp with time zone")]
    public DateTime? LastEditedAt { get; set; }

    [Column("submitted_at", TypeName = "timestamp with time zone")]
    public DateTime? SubmittedAt { get; set; }

    [Column("submitted_by")]
    public Guid? SubmittedBy { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("MissionTeamId")]
    [InverseProperty("MissionTeamReport")]
    public virtual MissionTeam? MissionTeam { get; set; }

    [ForeignKey("SubmittedBy")]
    public virtual User? SubmittedByUser { get; set; }

    [InverseProperty("MissionTeamReport")]
    public virtual ICollection<MissionActivityReport> MissionActivityReports { get; set; } = new List<MissionActivityReport>();

    [InverseProperty("MissionTeamReport")]
    public virtual ICollection<MissionTeamMemberEvaluation> MissionTeamMemberEvaluations { get; set; } = new List<MissionTeamMemberEvaluation>();
}
