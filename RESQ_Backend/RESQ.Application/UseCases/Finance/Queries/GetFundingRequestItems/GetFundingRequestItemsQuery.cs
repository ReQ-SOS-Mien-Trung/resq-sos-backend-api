using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Finance.Queries.GetFundingRequests;

namespace RESQ.Application.UseCases.Finance.Queries.GetFundingRequestItems;

public record GetFundingRequestItemsQuery(
    int FundingRequestId,
    int PageNumber,
    int PageSize
) : IRequest<PagedResult<FundingRequestItemListDto>>;
