using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.Repositories.Finance;

public interface IDonationRepository
{
    Task<PagedResult<DonationModel>> GetPagedAsync(int pageNumber, int pageSize, int? campaignId = null, CancellationToken cancellationToken = default);
    Task<DonationModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<DonationModel?> GetByPayosOrderIdAsync(string? orderId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Finds pending donations created before the specified time threshold.
    /// </summary>
    Task<List<DonationModel>> GetPendingDonationsOlderThanAsync(DateTime threshold, CancellationToken cancellationToken = default);
    
    Task CreateAsync(DonationModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(DonationModel model, CancellationToken cancellationToken = default);
}
