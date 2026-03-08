using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Infrastructure.Services.Finance;

public class DonationExpirationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DonationExpirationBackgroundService> _logger;

    // Check every 1 minute
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
    
    // Expire after 5 minutes + 1 minute buffer (to ensure user truly timed out on PayOS side)
    private readonly TimeSpan _expirationThreshold = TimeSpan.FromMinutes(6);

    public DonationExpirationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DonationExpirationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Donation Expiration Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredDonationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing expired donations.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Donation Expiration Service stopped.");
    }

    private async Task ProcessExpiredDonationsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var donationRepository = scope.ServiceProvider.GetRequiredService<IDonationRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var thresholdTime = DateTime.UtcNow.Subtract(_expirationThreshold);

        var expiredDonations = await donationRepository.GetPendingDonationsOlderThanAsync(thresholdTime, stoppingToken);

        if (expiredDonations.Any())
        {
            _logger.LogInformation("Found {Count} expired pending donations. Updating status to Failed...", expiredDonations.Count);

            foreach (var donation in expiredDonations)
            {
                // Use the Domain Method to update status instead of direct property setter
                donation.UpdatePaymentStatus(PayOSStatus.Failed);
                
                donation.PaymentAuditInfo += " [System: Expired due to timeout]";
                
                // Update in DB
                await donationRepository.UpdateAsync(donation, stoppingToken);
            }

            await unitOfWork.SaveAsync();
        }
    }
}
