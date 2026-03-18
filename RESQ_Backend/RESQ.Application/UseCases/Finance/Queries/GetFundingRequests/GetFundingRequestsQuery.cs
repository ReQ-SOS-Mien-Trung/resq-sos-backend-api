using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Finance.Queries.GetFundingRequests;

public record GetFundingRequestsQuery(
    int PageNumber,
    int PageSize,
    int? DepotId = null,
    string? Status = null
) : IRequest<PagedResult<FundingRequestListDto>>;
