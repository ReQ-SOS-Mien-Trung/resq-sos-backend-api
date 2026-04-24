using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.Repositories.Finance;

public interface IFundCampaignRepository
{
    Task<FundCampaignModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<FundCampaignModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<PagedResult<FundCampaignModel>> GetPagedAsync(int pageNumber, int pageSize, List<FundCampaignStatus>? statuses = null, CancellationToken cancellationToken = default);
    Task<List<FundCampaignModel>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task CreateAsync(FundCampaignModel campaign, CancellationToken cancellationToken = default);
    Task UpdateAsync(FundCampaignModel campaign, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Lấy các chiến dịch Active đã quá ngày kết thúc (dùng cho auto-close job).</summary>
    Task<List<FundCampaignModel>> GetExpiredActiveAsync(CancellationToken cancellationToken = default);
}
