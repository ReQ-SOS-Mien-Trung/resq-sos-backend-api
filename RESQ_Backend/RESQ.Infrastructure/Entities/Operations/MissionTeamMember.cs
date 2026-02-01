using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Entities.Operations;

[Table("mission_team_members")]
public partial class MissionTeamMember
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("mission_team_id")]
    public int? MissionTeamId { get; set; }

    [Column("rescuer_id")]
    public Guid? RescuerId { get; set; }

    [Column("role_in_team")]
    [StringLength(50)]
    public string? RoleInTeam { get; set; }

    [Column("joined_at", TypeName = "timestamp with time zone")]
    public DateTime? JoinedAt { get; set; }

    [Column("left_at", TypeName = "timestamp with time zone")]
    public DateTime? LeftAt { get; set; }

    [ForeignKey("MissionTeamId")]
    [InverseProperty("MissionTeamMembers")]
    public virtual MissionTeam? MissionTeam { get; set; }

    [ForeignKey("RescuerId")]
    [InverseProperty("MissionTeamMembers")]
    public virtual User? Rescuer { get; set; }
}