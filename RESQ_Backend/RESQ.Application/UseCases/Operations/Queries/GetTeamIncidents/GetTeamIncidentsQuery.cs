using MediatR;

namespace RESQ.Application.UseCases.Operations.Queries.GetTeamIncidents;

public record GetTeamIncidentsQuery(int MissionId) : IRequest<GetTeamIncidentsResponse>;
