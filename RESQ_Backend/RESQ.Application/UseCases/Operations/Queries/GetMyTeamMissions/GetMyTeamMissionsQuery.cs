using MediatR;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;

namespace RESQ.Application.UseCases.Operations.Queries.GetMyTeamMissions;

public record GetMyTeamMissionsQuery(Guid UserId) : IRequest<GetMissionsResponse>;
