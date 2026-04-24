using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.Repositories.Finance;

public interface IDonationRepository
{
    Task<PagedResult<DonationModel>> GetPagedAsync(
        int pageNumber, 
        int pageSize, 
        int? campaignId = null, 
        bool? isPrivate = null,
        CancellationToken cancellationToken = default);

    Task<DonationModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<DonationModel?> GetTrackedByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<DonationModel?> GetByOrderIdAsync(string? orderId, CancellationToken cancellationToken = default);
    Task<List<DonationModel>> GetPendingDonationsPastDeadlineAsync(DateTime currentTimeUtc, CancellationToken cancellationToken = default);
    Task CreateAsync(DonationModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(DonationModel model, CancellationToken cancellationToken = default);
}

