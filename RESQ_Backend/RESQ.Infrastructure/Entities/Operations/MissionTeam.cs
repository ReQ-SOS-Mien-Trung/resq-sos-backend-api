using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Infrastructure.Entities.Personnel;

namespace RESQ.Infrastructure.Entities.Operations;

[Table("mission_teams")]
public partial class MissionTeam
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("mission_id")]
    public int? MissionId { get; set; }

    [Column("rescuer_team_id")]
    public int? RescuerTeamId { get; set; }

    [Column("team_type")]
    [StringLength(50)]
    public string? TeamType { get; set; }

    [Column("current_location", TypeName = "geography(Point,4326)")]
    public Point? CurrentLocation { get; set; }

    [Column("location_updated_at", TypeName = "timestamp with time zone")]
    public DateTime? LocationUpdatedAt { get; set; }

    [Column("location_source")]
    public string? LocationSource { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("assigned_at", TypeName = "timestamp with time zone")]
    public DateTime? AssignedAt { get; set; }

    [Column("unassigned_at", TypeName = "timestamp with time zone")]
    public DateTime? UnassignedAt { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("MissionId")]
    [InverseProperty("MissionTeams")]
    public virtual Mission? Mission { get; set; }

    [ForeignKey("RescuerTeamId")]
    [InverseProperty("MissionTeams")]
    public virtual RescueTeam? RescuerTeam { get; set; }

    [InverseProperty("MissionTeam")]
    public virtual ICollection<MissionTeamMember> MissionTeamMembers { get; set; } = new List<MissionTeamMember>();

    [InverseProperty("MissionTeam")]
    public virtual ICollection<TeamIncident> TeamIncidents { get; set; } = new List<TeamIncident>();
}
