using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace RESQ.Infrastructure.Entities;

[Table("mission_activities")]
public partial class MissionActivity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("mission_id")]
    public int? MissionId { get; set; }

    [Column("step")]
    public int? Step { get; set; }

    [Column("activity_code")]
    [StringLength(50)]
    public string? ActivityCode { get; set; }

    [Column("activity_type")]
    [StringLength(50)]
    public string? ActivityType { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("target", TypeName = "jsonb")]
    public string? Target { get; set; }

    [Column("items", TypeName = "jsonb")]
    public string? Items { get; set; }

    [Column("target_location", TypeName = "geography(Point,4326)")]
    public Point? TargetLocation { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("assigned_at", TypeName = "timestamp with time zone")]
    public DateTime? AssignedAt { get; set; }

    [Column("completed_at", TypeName = "timestamp with time zone")]
    public DateTime? CompletedAt { get; set; }

    [Column("last_decision_by")]
    public Guid? LastDecisionBy { get; set; }

    [ForeignKey("LastDecisionBy")]
    [InverseProperty("MissionActivities")]
    public virtual User? LastDecisionByUser { get; set; }

    [ForeignKey("MissionId")]
    [InverseProperty("MissionActivities")]
    public virtual Mission? Mission { get; set; }
}
