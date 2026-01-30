using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities;

[Table("activity_ai_suggestions")]
public partial class ActivityAiSuggestion
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("cluster_id")]
    public int? ClusterId { get; set; }

    [Column("parent_mission_suggestion_id")]
    public int? ParentMissionSuggestionId { get; set; }

    [Column("adopted_activity_id")]
    public int? AdoptedActivityId { get; set; }

    [Column("model_name")]
    [StringLength(50)]
    public string? ModelName { get; set; }

    [Column("model_version")]
    [StringLength(50)]
    public string? ModelVersion { get; set; }

    [Column("activity_type")]
    [StringLength(50)]
    public string? ActivityType { get; set; }

    [Column("suggestion_phase")]
    [StringLength(50)]
    public string? SuggestionPhase { get; set; }

    [Column("suggested_actions", TypeName = "jsonb")]
    public string? SuggestedActions { get; set; }

    [Column("confidence_score")]
    public double? ConfidenceScore { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("adopted_at", TypeName = "timestamp with time zone")]
    public DateTime? AdoptedAt { get; set; }

    [ForeignKey("ClusterId")]
    [InverseProperty("ActivityAiSuggestions")]
    public virtual SosCluster? Cluster { get; set; }
}
