using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetSystemFundTransactions;

/// <summary>
/// Handler: Lấy lịch sử giao dịch quỹ hệ thống (phân trang).
/// </summary>
public class GetSystemFundTransactionsHandler(ISystemFundRepository systemFundRepo)
    : IRequestHandler<GetSystemFundTransactionsQuery, PagedResult<SystemFundTransactionDto>>
{
    private readonly ISystemFundRepository _systemFundRepo = systemFundRepo;

    public async Task<PagedResult<SystemFundTransactionDto>> Handle(
        GetSystemFundTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var pagedResult = await _systemFundRepo.GetPagedTransactionsAsync(
            request.PageNumber,
            request.PageSize,
            request.FromDate,
            request.ToDate,
            request.MinAmount,
            request.MaxAmount,
            request.TransactionTypes,
            request.Search,
            cancellationToken);

        var dtos = pagedResult.Items.Select(t => new SystemFundTransactionDto
        {
            Id = t.Id,
            SystemFundId = t.SystemFundId,
            TransactionType = t.TransactionType.ToString(),
            Amount = t.Amount,
            ReferenceType = t.ReferenceType,
            ReferenceId = t.ReferenceId,
            Note = t.Note,
            CreatedBy = t.CreatedBy,
            CreatedAt = t.CreatedAt.ToVietnamTime()
        }).ToList();

        return new PagedResult<SystemFundTransactionDto>(
            dtos,
            pagedResult.TotalCount,
            pagedResult.PageNumber,
            pagedResult.PageSize);
    }
}
