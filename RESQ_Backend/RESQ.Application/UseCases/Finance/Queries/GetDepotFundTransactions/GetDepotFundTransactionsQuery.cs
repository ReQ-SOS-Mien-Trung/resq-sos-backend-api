using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Finance.Queries.GetDepotFundTransactions;

/// <summary>
/// [Admin] Lấy lịch sử giao dịch quỹ của một kho theo depot ID.
/// </summary>
public record GetDepotFundTransactionsQuery(
    int DepotId,
    int PageNumber,
    int PageSize
) : IRequest<PagedResult<DepotFundTransactionDto>>;
