using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetDepotFundTransactions;

public class GetDepotFundTransactionsHandler(IDepotFundRepository depotFundRepo)
    : IRequestHandler<GetDepotFundTransactionsQuery, PagedResult<DepotFundTransactionDto>>
{
    private readonly IDepotFundRepository _depotFundRepo = depotFundRepo;

    public async Task<PagedResult<DepotFundTransactionDto>> Handle(
        GetDepotFundTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var pagedResult = await _depotFundRepo.GetPagedTransactionsByDepotIdAsync(
            request.DepotId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtos = pagedResult.Items.Select(t => new DepotFundTransactionDto
        {
            Id = t.Id,
            DepotFundId = t.DepotFundId,
            TransactionType = t.TransactionType.ToString(),
            Amount = t.Amount,
            ReferenceType = t.ReferenceType,
            ReferenceId = t.ReferenceId,
            Note = t.Note,
            CreatedBy = t.CreatedBy,
            CreatedAt = t.CreatedAt.ToVietnamTime()
        }).ToList();

        return new PagedResult<DepotFundTransactionDto>(
            dtos,
            pagedResult.TotalCount,
            pagedResult.PageNumber,
            pagedResult.PageSize);
    }
}
