using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;

public record GetSosClustersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    int? SosRequestId = null
) : IRequest<PagedResult<SosClusterDto>>;
