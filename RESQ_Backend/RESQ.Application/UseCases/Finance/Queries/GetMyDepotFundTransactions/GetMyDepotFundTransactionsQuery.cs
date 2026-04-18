using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Finance.Queries.GetDepotFundTransactions;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetMyDepotFundTransactions;

/// <summary>
/// [Manager] Lấy lịch sử giao dịch quỹ của kho mình đang quản lý.
/// </summary>
public record GetMyDepotFundTransactionsQuery(
    Guid UserId,
    int PageNumber,
    int PageSize,
    int DepotId,
    DateOnly? FromDate = null,
    DateOnly? ToDate   = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    List<DepotFundReferenceType>? ReferenceTypes = null,
    string? Search     = null
) : IRequest<PagedResult<DepotFundTransactionDto>>;
