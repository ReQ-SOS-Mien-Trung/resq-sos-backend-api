using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("mission_activities")]
public partial class MissionActivity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("assigned_unit_id")]
    public int? AssignedUnitId { get; set; }

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

    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("assigned_at", TypeName = "timestamp without time zone")]
    public DateTime? AssignedAt { get; set; }

    [Column("completed_at", TypeName = "timestamp without time zone")]
    public DateTime? CompletedAt { get; set; }

    [Column("last_decision_by")]
    public Guid? LastDecisionBy { get; set; }

    [InverseProperty("Activity")]
    public virtual ICollection<ActivityHandoverLog> ActivityHandoverLogs { get; set; } = new List<ActivityHandoverLog>();

    [ForeignKey("AssignedUnitId")]
    [InverseProperty("MissionActivities")]
    public virtual RescueUnit? AssignedUnit { get; set; }

    [ForeignKey("LastDecisionBy")]
    [InverseProperty("MissionActivities")]
    public virtual User? LastDecisionByNavigation { get; set; }

    [ForeignKey("MissionId")]
    [InverseProperty("MissionActivities")]
    public virtual Mission? Mission { get; set; }
}
