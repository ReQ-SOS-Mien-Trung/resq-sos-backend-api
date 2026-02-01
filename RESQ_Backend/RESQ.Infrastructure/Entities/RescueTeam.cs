using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace RESQ.Infrastructure.Entities;

[Table("rescue_teams")]
public partial class RescueTeam
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("assembly_point_id")]
    public int? AssemblyPointId { get; set; }

    [Column("code")]
    [StringLength(50)]
    public string? Code { get; set; }

    [Column("name")]
    [StringLength(255)]
    public string? Name { get; set; }

    [Column("team_type")]
    [StringLength(50)]
    public string? TeamType { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("location", TypeName = "geography(Point,4326)")]
    public Point? Location { get; set; }

    [Column("max_members")]
    public int? MaxMembers { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime? UpdatedAt { get; set; }

    [Column("disband_at", TypeName = "timestamp with time zone")]
    public DateTime? DisbandAt { get; set; }

    [ForeignKey("AssemblyPointId")]
    [InverseProperty("RescueTeams")]
    public virtual AssemblyPoint? AssemblyPoint { get; set; }

    [InverseProperty("RescuerTeam")]
    public virtual ICollection<MissionTeam> MissionTeams { get; set; } = new List<MissionTeam>();

    [InverseProperty("AdoptedRescueTeam")]
    public virtual ICollection<RescueTeamAiSuggestion> RescueTeamAiSuggestions { get; set; } = new List<RescueTeamAiSuggestion>();
}
