using MediatR;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;

public record GetSosClustersQuery : IRequest<GetSosClustersResponse>;
