using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.Repositories.Finance;

public interface IFundTransactionRepository
{
    Task<PagedResult<FundTransactionModel>> GetByCampaignIdAsync(int campaignId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task CreateAsync(FundTransactionModel transaction, CancellationToken cancellationToken = default);
}
