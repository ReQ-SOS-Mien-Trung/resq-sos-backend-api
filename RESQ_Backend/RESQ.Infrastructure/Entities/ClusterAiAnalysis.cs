using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("cluster_ai_analysis")]
public partial class ClusterAiAnalysis
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("cluster_id")]
    public int? ClusterId { get; set; }

    [Column("model_name")]
    [StringLength(50)]
    public string? ModelName { get; set; }

    [Column("model_version")]
    [StringLength(50)]
    public string? ModelVersion { get; set; }

    [Column("analysis_type")]
    [StringLength(50)]
    public string? AnalysisType { get; set; }

    [Column("suggested_severity_level")]
    [StringLength(50)]
    public string? SuggestedSeverityLevel { get; set; }

    [Column("suggested_mission_types")]
    [StringLength(255)]
    public string? SuggestedMissionTypes { get; set; }

    [Column("confidence_score")]
    public double? ConfidenceScore { get; set; }

    [Column("suggestion_scope")]
    [StringLength(100)]
    public string? SuggestionScope { get; set; }

    [Column("metadata", TypeName = "jsonb")]
    public string? Metadata { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("adopted_at", TypeName = "timestamp with time zone")]
    public DateTime? AdoptedAt { get; set; }

    [ForeignKey("ClusterId")]
    [InverseProperty("ClusterAiAnalyses")]
    public virtual SosCluster? Cluster { get; set; }
}
