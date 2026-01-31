using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("missions")]
public partial class Mission
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("cluster_id")]
    public int? ClusterId { get; set; }

    [Column("previous_mission_id")]
    public int? PreviousMissionId { get; set; }

    [Column("mission_type")]
    [StringLength(50)]
    public string? MissionType { get; set; }

    [Column("priority_score")]
    public double? PriorityScore { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("start_time", TypeName = "timestamp with time zone")]
    public DateTime? StartTime { get; set; }

    [Column("expected_end_time", TypeName = "timestamp with time zone")]
    public DateTime? ExpectedEndTime { get; set; }

    [Column("is_completed")]
    public bool? IsCompleted { get; set; }

    [Column("created_by")]
    public Guid? CreatedById { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("completed_at", TypeName = "timestamp with time zone")]
    public DateTime? CompletedAt { get; set; }

    [ForeignKey("ClusterId")]
    [InverseProperty("Missions")]
    public virtual SosCluster? Cluster { get; set; }

    [ForeignKey("CreatedById")]
    [InverseProperty("Missions")]
    public virtual User? CreatedBy { get; set; }

    [ForeignKey("PreviousMissionId")]
    [InverseProperty("InversePreviousMission")]
    public virtual Mission? PreviousMission { get; set; }

    [InverseProperty("PreviousMission")]
    public virtual ICollection<Mission> InversePreviousMission { get; set; } = new List<Mission>();

    [InverseProperty("Mission")]
    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    [InverseProperty("Mission")]
    public virtual ICollection<MissionActivity> MissionActivities { get; set; } = new List<MissionActivity>();

    [InverseProperty("Mission")]
    public virtual ICollection<MissionItem> MissionItems { get; set; } = new List<MissionItem>();

    [InverseProperty("Mission")]
    public virtual ICollection<MissionTeam> MissionTeams { get; set; } = new List<MissionTeam>();

    [InverseProperty("Mission")]
    public virtual ICollection<MissionVehicle> MissionVehicles { get; set; } = new List<MissionVehicle>();
}
