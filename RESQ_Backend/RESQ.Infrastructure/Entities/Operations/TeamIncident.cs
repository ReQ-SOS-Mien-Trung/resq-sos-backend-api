using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace RESQ.Infrastructure.Entities.Operations;

[Table("team_incidents")]
public partial class TeamIncident
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("mission_team_id")]
    public int? MissionTeamId { get; set; }

    [Column("location", TypeName = "geography(Point,4326)")]
    public Point? Location { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("reported_by")]
    public Guid? ReportedBy { get; set; }

    [Column("reported_at", TypeName = "timestamp with time zone")]
    public DateTime? ReportedAt { get; set; }

    [ForeignKey("MissionTeamId")]
    [InverseProperty("TeamIncidents")]
    public virtual MissionTeam? MissionTeam { get; set; }
}