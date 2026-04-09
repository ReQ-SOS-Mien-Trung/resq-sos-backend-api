using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Finance.Queries.GetSystemFundTransactions;

/// <summary>
/// [Admin] Lấy lịch sử giao dịch quỹ hệ thống (phân trang).
/// </summary>
public record GetSystemFundTransactionsQuery(
    int PageNumber,
    int PageSize
) : IRequest<PagedResult<SystemFundTransactionDto>>;
