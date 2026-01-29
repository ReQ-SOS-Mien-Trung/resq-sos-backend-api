using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities;

namespace RESQ.Infrastructure.Entities;

[Table("missions")]
public partial class Mission
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("cluster_id")]
    public int? ClusterId { get; set; }

    [Column("mission_type")]
    [StringLength(50)]
    public string? MissionType { get; set; }

    [Column("priority")]
    [StringLength(50)]
    public string? Priority { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("start_time", TypeName = "timestamp with time zone")]
    public DateTime? StartTime { get; set; }

    [Column("expected_end_time", TypeName = "timestamp with time zone")]
    public DateTime? ExpectedEndTime { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("completed_at", TypeName = "timestamp with time zone")]
    public DateTime? CompletedAt { get; set; }

    [Column("coordinator_id")]
    public Guid? CoordinatorId { get; set; }

    [Column("primary_unit_id")]
    public int? PrimaryUnitId { get; set; }

    [ForeignKey("ClusterId")]
    [InverseProperty("Missions")]
    public virtual SosCluster? Cluster { get; set; }

    [InverseProperty("Mission")]
    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    [ForeignKey("CoordinatorId")]
    [InverseProperty("Missions")]
    public virtual User? Coordinator { get; set; }

    [InverseProperty("Mission")]
    public virtual ICollection<MissionActivity> MissionActivities { get; set; } = new List<MissionActivity>();

    [InverseProperty("Mission")]
    public virtual ICollection<MissionItem> MissionItems { get; set; } = new List<MissionItem>();

    [ForeignKey("PrimaryUnitId")]
    [InverseProperty("Missions")]
    public virtual RescueUnit? PrimaryUnit { get; set; }
}
