using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetSystemFundTransactions;

/// <summary>
/// [Admin] Lấy lịch sử giao dịch quỹ hệ thống (phân trang).
/// </summary>
public record GetSystemFundTransactionsQuery(
    int PageNumber,
    int PageSize,
    DateOnly? FromDate   = null,
    DateOnly? ToDate     = null,
    decimal? MinAmount   = null,
    decimal? MaxAmount   = null,
    List<SystemFundTransactionType>? TransactionTypes = null,
    string? Search       = null
) : IRequest<PagedResult<SystemFundTransactionDto>>;
