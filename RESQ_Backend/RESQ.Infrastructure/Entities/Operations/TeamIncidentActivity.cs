using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Operations;

[Table("team_incident_activities")]
public partial class TeamIncidentActivity
{
    [Column("team_incident_id")]
    public int TeamIncidentId { get; set; }

    [Column("mission_activity_id")]
    public int MissionActivityId { get; set; }

    [Column("order_index")]
    public int OrderIndex { get; set; }

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [ForeignKey(nameof(TeamIncidentId))]
    [InverseProperty(nameof(Operations.TeamIncident.TeamIncidentActivities))]
    public virtual TeamIncident? TeamIncident { get; set; }

    [ForeignKey(nameof(MissionActivityId))]
    [InverseProperty(nameof(Operations.MissionActivity.TeamIncidentActivities))]
    public virtual MissionActivity? MissionActivity { get; set; }
}