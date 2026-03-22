using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetFundingRequests;

public record GetFundingRequestsQuery(
    int PageNumber,
    int PageSize,
    List<int>? DepotIds = null,
    List<FundingRequestStatus>? Statuses = null
) : IRequest<PagedResult<FundingRequestListDto>>;
