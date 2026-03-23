using MediatR;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetCampaignTransactions;

public class GetCampaignTransactionsHandler(IFundTransactionRepository transactionRepo)
    : IRequestHandler<GetCampaignTransactionsQuery, PagedResult<FundTransactionDto>>
{
    private readonly IFundTransactionRepository _transactionRepo = transactionRepo;

    public async Task<PagedResult<FundTransactionDto>> Handle(
        GetCampaignTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var pagedResult = await _transactionRepo.GetByCampaignIdAsync(
            request.CampaignId,
            request.PageNumber,
            request.PageSize,
            request.Types,
            request.Directions,
            request.ReferenceTypes,
            cancellationToken);

        var dtos = pagedResult.Items.Select(t => new FundTransactionDto
        {
            Id = t.Id,
            FundCampaignId = t.FundCampaignId,
            FundCampaignName = t.FundCampaignName,
            Type = FinanceLabels.Translate(FinanceLabels.TransactionTypeLabels, t.Type.ToString()),
            Direction = FinanceLabels.Translate(FinanceLabels.DirectionLabels, t.Direction.ToString()),
            Amount = t.Amount,
            ReferenceType = FinanceLabels.Translate(FinanceLabels.TransactionReferenceTypeLabels, t.ReferenceType.ToString()),
            ReferenceId = t.ReferenceId,
            CreatedAt = t.CreatedAt
        }).ToList();

        return new PagedResult<FundTransactionDto>(
            dtos,
            pagedResult.TotalCount,
            pagedResult.PageNumber,
            pagedResult.PageSize);
    }
}
