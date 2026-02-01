using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("mission_ai_suggestions")]
public partial class MissionAiSuggestion
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("cluster_id")]
    public int? ClusterId { get; set; }

    [Column("adopted_mission_id")]
    public int? AdoptedMissionId { get; set; }

    [Column("model_name")]
    [StringLength(50)]
    public string? ModelName { get; set; }

    [Column("model_version")]
    [StringLength(50)]
    public string? ModelVersion { get; set; }

    [Column("analysis_type")]
    [StringLength(50)]
    public string? AnalysisType { get; set; }

    [Column("suggested_mission_title")]
    [StringLength(255)]
    public string? SuggestedMissionTitle { get; set; }

    [Column("suggested_priority_score")]
    public double? SuggestedPriorityScore { get; set; }

    [Column("suggested_primary_team_id")]
    public int? SuggestedPrimaryTeamId { get; set; }

    [Column("suggested_depot_ids", TypeName = "jsonb")]
    public string? SuggestedDepotIds { get; set; }

    [Column("confidence_score")]
    public double? ConfidenceScore { get; set; }

    [Column("suggestion_scope")]
    public string? SuggestionScope { get; set; }

    [Column("metadata", TypeName = "jsonb")]
    public string? Metadata { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("adopted_at", TypeName = "timestamp with time zone")]
    public DateTime? AdoptedAt { get; set; }

    [ForeignKey("ClusterId")]
    [InverseProperty("MissionAiSuggestions")]
    public virtual SosCluster? Cluster { get; set; }
}
