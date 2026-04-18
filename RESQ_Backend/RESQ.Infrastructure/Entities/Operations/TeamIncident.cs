using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Infrastructure.Entities.Emergency;

namespace RESQ.Infrastructure.Entities.Operations;

[Table("team_incidents")]
public partial class TeamIncident
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("mission_team_id")]
    public int? MissionTeamId { get; set; }

    [Column("mission_activity_id")]
    public int? MissionActivityId { get; set; }

    [Column("location", TypeName = "geography(Point,4326)")]
    public Point? Location { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("incident_scope")]
    [StringLength(50)]
    public string? IncidentScope { get; set; }

    [Column("incident_type")]
    [StringLength(50)]
    public string? IncidentType { get; set; }

    [Column("decision_code")]
    [StringLength(100)]
    public string? DecisionCode { get; set; }

    [Column("detail_json", TypeName = "jsonb")]
    public string? DetailJson { get; set; }

    [Column("payload_version")]
    public int? PayloadVersion { get; set; }

    [Column("need_support_sos")]
    public bool? NeedSupportSos { get; set; }

    [Column("need_reassign_activity")]
    public bool? NeedReassignActivity { get; set; }

    [Column("support_sos_request_id")]
    public int? SupportSosRequestId { get; set; }

    [Column("reported_by")]
    public Guid? ReportedBy { get; set; }

    [Column("reported_at", TypeName = "timestamp with time zone")]
    public DateTime? ReportedAt { get; set; }

    [ForeignKey("MissionTeamId")]
    [InverseProperty("TeamIncidents")]
    public virtual MissionTeam? MissionTeam { get; set; }

    [ForeignKey("MissionActivityId")]
    public virtual MissionActivity? MissionActivity { get; set; }

    [ForeignKey("SupportSosRequestId")]
    public virtual SosRequest? SupportSosRequest { get; set; }

    [InverseProperty(nameof(Operations.TeamIncidentActivity.TeamIncident))]
    public virtual ICollection<TeamIncidentActivity> TeamIncidentActivities { get; set; } = new List<TeamIncidentActivity>();
}
