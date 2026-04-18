using MediatR;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissions;

public record GetMissionsQuery(int? ClusterId) : IRequest<GetMissionsResponse>;
