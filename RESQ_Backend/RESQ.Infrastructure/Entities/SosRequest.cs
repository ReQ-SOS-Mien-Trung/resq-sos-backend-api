using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace RESQ.Infrastructure.Entities;

[Table("sos_requests")]
public partial class SosRequest
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("cluster_id")]
    public int? ClusterId { get; set; }

    [Column("user_id")]
    public Guid? UserId { get; set; }

    [Column("location", TypeName = "geography(Point,4326)")]
    public Point? Location { get; set; }

    [Column("raw_message")]
    public string? RawMessage { get; set; }

    [Column("priority_level")]
    [StringLength(10)]
    public string? PriorityLevel { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("ai_analysis", TypeName = "jsonb")]
    public string? AiAnalysis { get; set; }

    [Column("wait_time_minutes")]
    public int? WaitTimeMinutes { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("last_updated_at", TypeName = "timestamp with time zone")]
    public DateTime? LastUpdatedAt { get; set; }

    [Column("reviewed_at", TypeName = "timestamp with time zone")]
    public DateTime? ReviewedAt { get; set; }

    [Column("reviewed_by")]
    public Guid? ReviewedById { get; set; }

    [ForeignKey("ClusterId")]
    [InverseProperty("SosRequests")]
    public virtual SosCluster? Cluster { get; set; }

    [ForeignKey("ReviewedById")]
    [InverseProperty("ReviewedSosRequests")]
    public virtual User? ReviewedBy { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("SosRequests")]
    public virtual User? User { get; set; }

    [InverseProperty("SosRequest")]
    public virtual ICollection<SosAiAnalysis> SosAiAnalyses { get; set; } = new List<SosAiAnalysis>();

    [InverseProperty("SosRequest")]
    public virtual ICollection<SosRequestUpdate> SosRequestUpdates { get; set; } = new List<SosRequestUpdate>();

    [InverseProperty("SosRequest")]
    public virtual ICollection<SosRuleEvaluation> SosRuleEvaluations { get; set; } = new List<SosRuleEvaluation>();
}
