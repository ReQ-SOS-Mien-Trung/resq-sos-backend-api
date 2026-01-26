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

    [Column("event_assessment", TypeName = "jsonb")]
    public string? EventAssessment { get; set; }

    [Column("suggested_mission_plan", TypeName = "jsonb")]
    public string? SuggestedMissionPlan { get; set; }

    [Column("confidence_score")]
    public double? ConfidenceScore { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("ClusterId")]
    [InverseProperty("ClusterAiAnalyses")]
    public virtual SosCluster? Cluster { get; set; }
}
