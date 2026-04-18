using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetDepotFundTransactions;

/// <summary>
/// [Admin] Lấy lịch sử giao dịch quỹ của một kho theo depot ID.
/// </summary>
public record GetDepotFundTransactionsQuery(
    int DepotId,
    int PageNumber,
    int PageSize,
    DateOnly? FromDate = null,
    DateOnly? ToDate   = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    List<DepotFundReferenceType>? ReferenceTypes = null,
    string? Search     = null
) : IRequest<PagedResult<DepotFundTransactionDto>>;
