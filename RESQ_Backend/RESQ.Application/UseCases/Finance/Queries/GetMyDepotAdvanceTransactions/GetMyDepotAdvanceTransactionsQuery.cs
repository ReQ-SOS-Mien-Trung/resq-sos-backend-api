using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Finance.Queries.GetDepotFundTransactions;

namespace RESQ.Application.UseCases.Finance.Queries.GetMyDepotAdvanceTransactions;

public record GetMyDepotAdvanceTransactionsQuery(
    Guid UserId,
    int PageNumber,
    int PageSize
) : IRequest<PagedResult<DepotFundTransactionDto>>;
