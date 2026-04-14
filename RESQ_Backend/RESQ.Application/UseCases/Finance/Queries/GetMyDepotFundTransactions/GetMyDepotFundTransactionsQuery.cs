using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Finance.Queries.GetDepotFundTransactions;

namespace RESQ.Application.UseCases.Finance.Queries.GetMyDepotFundTransactions;

/// <summary>
/// [Manager] Lấy lịch sử giao dịch quỹ của kho mình đang quản lý.
/// </summary>
public record GetMyDepotFundTransactionsQuery(
    Guid UserId,
    int PageNumber,
    int PageSize
, int? DepotId = null) : IRequest<PagedResult<DepotFundTransactionDto>>;
