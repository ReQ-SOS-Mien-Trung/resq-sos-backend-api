using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Personnel;

namespace RESQ.Infrastructure.Entities.Operations;

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

    [Column("completed_by")]
    public Guid? CompletedBy { get; set; }

    [Column("mission_team_id")]
    public int? MissionTeamId { get; set; }

    [Column("priority")]
    [StringLength(20)]
    public string? Priority { get; set; }

    [Column("estimated_time")]
    public int? EstimatedTime { get; set; }

    [Column("sos_request_id")]
    public int? SosRequestId { get; set; }

    [Column("depot_id")]
    public int? DepotId { get; set; }

    [Column("depot_name")]
    [StringLength(255)]
    public string? DepotName { get; set; }

    [Column("depot_address")]
    public string? DepotAddress { get; set; }

    [Column("assembly_point_id")]
    public int? AssemblyPointId { get; set; }

    [ForeignKey("LastDecisionBy")]
    [InverseProperty("MissionActivities")]
    public virtual User? LastDecisionByUser { get; set; }

    [ForeignKey("CompletedBy")]
    public virtual User? CompletedByUser { get; set; }

    [ForeignKey("MissionId")]
    [InverseProperty("MissionActivities")]
    public virtual Mission? Mission { get; set; }

    [ForeignKey("MissionTeamId")]
    public virtual MissionTeam? MissionTeam { get; set; }

    [ForeignKey("AssemblyPointId")]
    public virtual AssemblyPoint? AssemblyPoint { get; set; }
}
