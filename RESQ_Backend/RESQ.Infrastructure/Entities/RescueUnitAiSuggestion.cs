using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("rescue_unit_ai_suggestions")]
public partial class RescueUnitAiSuggestion
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("cluster_id")]
    public int? ClusterId { get; set; }

    [Column("suggested_rescue_unit_id")]
    public int? SuggestedRescueUnitId { get; set; }

    [Column("model_name")]
    [StringLength(50)]
    public string? ModelName { get; set; }

    [Column("model_version")]
    [StringLength(50)]
    public string? ModelVersion { get; set; }

    [Column("analysis_type")]
    [StringLength(50)]
    public string? AnalysisType { get; set; }

    [Column("assigned_reason", TypeName = "jsonb")]
    public string? AssignedReason { get; set; }

    [Column("confidence_score")]
    public double? ConfidenceScore { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("adopted_at", TypeName = "timestamp with time zone")]
    public DateTime? AdoptedAt { get; set; }

    [ForeignKey("ClusterId")]
    [InverseProperty("RescueUnitAiSuggestions")]
    public virtual SosCluster? Cluster { get; set; }

    [ForeignKey("SuggestedRescueUnitId")]
    [InverseProperty("RescueUnitAiSuggestions")]
    public virtual RescueUnit? SuggestedRescueUnit { get; set; }
}
