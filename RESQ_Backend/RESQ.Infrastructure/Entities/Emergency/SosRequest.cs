using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Entities.Emergency;

[Table("sos_requests")]
public partial class SosRequest
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("packet_id")]
    public Guid? PacketId { get; set; }

    [Column("cluster_id")]
    public int? ClusterId { get; set; }

    [Column("user_id")]
    public Guid? UserId { get; set; }

    [Column("location", TypeName = "geography(Point,4326)")]
    public Point? Location { get; set; }

    [Column("location_accuracy")]
    public double? LocationAccuracy { get; set; }

    [Column("sos_type")]
    [StringLength(50)]
    public string? SosType { get; set; }

    [Column("raw_message")]
    public string? RawMessage { get; set; }

    [Column("structured_data", TypeName = "jsonb")]
    public string? StructuredData { get; set; }

    [Column("network_metadata", TypeName = "jsonb")]
    public string? NetworkMetadata { get; set; }

    [Column("sender_info", TypeName = "jsonb")]
    public string? SenderInfo { get; set; }

    [Column("victim_info", TypeName = "jsonb")]
    public string? VictimInfo { get; set; }

    [Column("reporter_info", TypeName = "jsonb")]
    public string? ReporterInfo { get; set; }

    [Column("is_sent_on_behalf")]
    public bool IsSentOnBehalf { get; set; }

    [Column("origin_id")]
    [StringLength(255)]
    public string? OriginId { get; set; }

    [Column("priority_level")]
    [StringLength(10)]
    public string? PriorityLevel { get; set; }

    [Column("priority_score")]
    public double? PriorityScore { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("ai_analysis", TypeName = "jsonb")]
    public string? AiAnalysis { get; set; }

    /// <summary>Thời điểm server nhận được SOS request (có thể muộn hơn CreatedAt nếu gửi qua mesh network offline).</summary>
    [Column("received_at", TypeName = "timestamp with time zone")]
    public DateTime? ReceivedAt { get; set; }

    [Column("timestamp")]
    public long? Timestamp { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("last_updated_at", TypeName = "timestamp with time zone")]
    public DateTime? LastUpdatedAt { get; set; }

    [Column("reviewed_at", TypeName = "timestamp with time zone")]
    public DateTime? ReviewedAt { get; set; }

    [Column("reviewed_by")]
    public Guid? ReviewedById { get; set; }

    [Column("created_by_coordinator_id")]
    public Guid? CreatedByCoordinatorId { get; set; }

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

    [InverseProperty("SosRequest")]
    public virtual ICollection<SosRequestCompanion> Companions { get; set; } = new List<SosRequestCompanion>();
}
