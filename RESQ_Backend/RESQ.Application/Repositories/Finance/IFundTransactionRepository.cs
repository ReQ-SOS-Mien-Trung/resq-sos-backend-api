using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.Repositories.Finance;

public interface IFundTransactionRepository
{
    Task<PagedResult<FundTransactionModel>> GetByCampaignIdAsync(
        int campaignId,
        int pageNumber,
        int pageSize,
        List<TransactionType>?          types          = null,
        List<TransactionDirection>?     directions     = null,
        List<TransactionReferenceType>? referenceTypes = null,
        CancellationToken cancellationToken = default);
    Task CreateAsync(FundTransactionModel transaction, CancellationToken cancellationToken = default);
}
