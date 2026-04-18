using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Entities.Operations;

[Table("mission_team_member_evaluations")]
public class MissionTeamMemberEvaluation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("mission_team_report_id")]
    public int MissionTeamReportId { get; set; }

    [Column("rescuer_id")]
    public Guid RescuerId { get; set; }

    [Column("response_time_score", TypeName = "numeric(5,2)")]
    public decimal ResponseTimeScore { get; set; }

    [Column("rescue_effectiveness_score", TypeName = "numeric(5,2)")]
    public decimal RescueEffectivenessScore { get; set; }

    [Column("decision_handling_score", TypeName = "numeric(5,2)")]
    public decimal DecisionHandlingScore { get; set; }

    [Column("safety_medical_skill_score", TypeName = "numeric(5,2)")]
    public decimal SafetyMedicalSkillScore { get; set; }

    [Column("teamwork_communication_score", TypeName = "numeric(5,2)")]
    public decimal TeamworkCommunicationScore { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("MissionTeamReportId")]
    [InverseProperty("MissionTeamMemberEvaluations")]
    public virtual MissionTeamReport? MissionTeamReport { get; set; }

    [ForeignKey("RescuerId")]
    [InverseProperty("MissionTeamMemberEvaluations")]
    public virtual RescuerProfile? RescuerProfile { get; set; }
}
