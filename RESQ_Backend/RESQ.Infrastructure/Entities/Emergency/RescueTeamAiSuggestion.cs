using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Personnel;

namespace RESQ.Infrastructure.Entities.Emergency;

[Table("rescue_team_ai_suggestions")]
public partial class RescueTeamAiSuggestion
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("cluster_id")]
    public int? ClusterId { get; set; }

    [Column("adopted_rescue_team_id")]
    public int? AdoptedRescueTeamId { get; set; }

    [Column("model_name")]
    [StringLength(50)]
    public string? ModelName { get; set; }

    [Column("model_version")]
    [StringLength(50)]
    public string? ModelVersion { get; set; }

    [Column("analysis_type")]
    [StringLength(50)]
    public string? AnalysisType { get; set; }

    [Column("suggested_members", TypeName = "jsonb")]
    public string? SuggestedMembers { get; set; }

    [Column("confidence_score")]
    public double? ConfidenceScore { get; set; }

    [Column("suggestion_scope")]
    public string? SuggestionScope { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("adopted_at", TypeName = "timestamp with time zone")]
    public DateTime? AdoptedAt { get; set; }

    [ForeignKey("AdoptedRescueTeamId")]
    [InverseProperty("RescueTeamAiSuggestions")]
    public virtual RescueTeam? AdoptedRescueTeam { get; set; }

    [ForeignKey("ClusterId")]
    [InverseProperty("RescueTeamAiSuggestions")]
    public virtual SosCluster? Cluster { get; set; }
}