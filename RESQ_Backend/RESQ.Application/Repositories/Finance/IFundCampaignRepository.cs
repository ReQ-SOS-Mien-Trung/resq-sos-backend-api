using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.Repositories.Finance;

public interface IFundCampaignRepository
{
    Task<FundCampaignModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<FundCampaignModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<PagedResult<FundCampaignModel>> GetPagedAsync(int pageNumber, int pageSize, string? status = null, CancellationToken cancellationToken = default);
    Task CreateAsync(FundCampaignModel campaign, CancellationToken cancellationToken = default);
    Task UpdateAsync(FundCampaignModel campaign, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
