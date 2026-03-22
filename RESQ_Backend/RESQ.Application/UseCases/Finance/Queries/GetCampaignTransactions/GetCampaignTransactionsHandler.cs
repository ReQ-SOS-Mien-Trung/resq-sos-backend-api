using MediatR;
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
            cancellationToken);

        var dtos = pagedResult.Items.Select(t => new FundTransactionDto
        {
            Id = t.Id,
            FundCampaignId = t.FundCampaignId,
            FundCampaignName = t.FundCampaignName,
            Type = t.Type.ToString(),
            Direction = t.Direction,
            Amount = t.Amount,
            ReferenceType = t.ReferenceType.ToString(),
            ReferenceId = t.ReferenceId,
            CreatedByUserName = t.CreatedByUserName,
            CreatedAt = t.CreatedAt
        }).ToList();

        return new PagedResult<FundTransactionDto>(
            dtos,
            pagedResult.TotalCount,
            pagedResult.PageNumber,
            pagedResult.PageSize);
    }
}
