using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("sos_ai_analysis")]
public partial class SosAiAnalysis
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("sos_request_id")]
    public int? SosRequestId { get; set; }

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

    [Column("suggested_priority")]
    [StringLength(50)]
    public string? SuggestedPriority { get; set; }

    [Column("explanation")]
    public string? Explanation { get; set; }

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

    [ForeignKey("SosRequestId")]
    [InverseProperty("SosAiAnalyses")]
    public virtual SosRequest? SosRequest { get; set; }
}
