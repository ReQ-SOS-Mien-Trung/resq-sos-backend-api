using MediatR;

namespace RESQ.Application.UseCases.Operations.Queries.GetTeamIncidentById;

public record GetTeamIncidentByIdQuery(int IncidentId) : IRequest<GetTeamIncidentByIdResponse>;
