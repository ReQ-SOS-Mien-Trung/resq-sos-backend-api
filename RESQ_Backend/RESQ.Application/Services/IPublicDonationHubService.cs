using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.Services;

public interface IPublicDonationHubService
{
    Task PushDonationSucceededAsync(
        DonationModel donation,
        CancellationToken cancellationToken = default);
}
