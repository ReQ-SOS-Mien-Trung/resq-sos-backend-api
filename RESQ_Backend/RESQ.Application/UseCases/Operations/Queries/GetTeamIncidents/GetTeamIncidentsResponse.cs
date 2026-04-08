using RESQ.Application.UseCases.Operations.Queries.Shared;

namespace RESQ.Application.UseCases.Operations.Queries.GetTeamIncidents;

public class GetTeamIncidentsResponse
{
    public int MissionId { get; set; }
    public List<TeamIncidentQueryDto> Incidents { get; set; } = [];
}
