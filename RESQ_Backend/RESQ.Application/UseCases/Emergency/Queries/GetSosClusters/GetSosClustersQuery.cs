using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Common.Sorting;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;

public record GetSosClustersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    int? SosRequestId = null,
    List<SosClusterStatus>? Statuses = null,
    List<SosPriorityLevel>? Priorities = null,
    List<SosRequestType>? SosTypes = null,
    IReadOnlyList<SosSortOption>? SortOptions = null
) : IRequest<PagedResult<SosClusterDto>>;
