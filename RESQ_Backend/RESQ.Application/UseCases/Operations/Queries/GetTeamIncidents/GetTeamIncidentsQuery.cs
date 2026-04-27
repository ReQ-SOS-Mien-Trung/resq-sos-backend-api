using MediatR;

namespace RESQ.Application.UseCases.Operations.Queries.GetTeamIncidents;

public class GetTeamIncidentsQuery(int missionId) : IRequest<GetTeamIncidentsResponse>
{
    public int MissionId { get; } = missionId;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
