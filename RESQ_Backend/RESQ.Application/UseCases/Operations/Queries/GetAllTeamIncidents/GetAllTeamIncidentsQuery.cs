using MediatR;

namespace RESQ.Application.UseCases.Operations.Queries.GetAllTeamIncidents;

public class GetAllTeamIncidentsQuery : IRequest<GetAllTeamIncidentsResponse>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
