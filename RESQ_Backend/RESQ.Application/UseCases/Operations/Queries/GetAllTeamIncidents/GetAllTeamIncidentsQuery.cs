using MediatR;

namespace RESQ.Application.UseCases.Operations.Queries.GetAllTeamIncidents;

public record GetAllTeamIncidentsQuery : IRequest<GetAllTeamIncidentsResponse>;
