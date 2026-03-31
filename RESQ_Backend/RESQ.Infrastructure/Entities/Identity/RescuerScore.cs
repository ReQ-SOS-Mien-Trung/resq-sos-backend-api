using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Identity;

[Table("rescuer_scores")]
public class RescuerScore
{
    [Key]
    [Column("user_id")]
    public Guid UserId { get; set; }

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

    [Column("overall_average_score", TypeName = "numeric(5,2)")]
    public decimal OverallAverageScore { get; set; }

    [Column("evaluation_count")]
    public int EvaluationCount { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("UserId")]
    public virtual RescuerProfile RescuerProfile { get; set; } = null!;
}
