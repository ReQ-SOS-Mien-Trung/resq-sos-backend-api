using RESQ.Application.UseCases.Operations.Queries.Shared;

namespace RESQ.Application.UseCases.Operations.Queries.GetAllTeamIncidents;

public class GetAllTeamIncidentsResponse
{
    public List<TeamIncidentQueryDto> Incidents { get; set; } = [];
}
