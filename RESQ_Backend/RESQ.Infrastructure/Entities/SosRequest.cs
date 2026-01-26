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

    [Column("rescue_message")]
    public string? RescueMessage { get; set; }

    [Column("priority_level")]
    [StringLength(10)]
    public string? PriorityLevel { get; set; }

    [Column("water_level")]
    [StringLength(50)]
    public string? WaterLevel { get; set; }

    [Column("victim_count")]
    public int? VictimCount { get; set; }

    [Column("is_analyzed")]
    public bool? IsAnalyzed { get; set; }

    [Column("wait_time_minutes")]
    public int? WaitTimeMinutes { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime? CreatedAt { get; set; }

    [InverseProperty("SosRequest")]
    public virtual ICollection<SosAiAnalysis> SosAiAnalyses { get; set; } = new List<SosAiAnalysis>();

    [ForeignKey("UserId")]
    [InverseProperty("SosRequests")]
    public virtual User? User { get; set; }
}
