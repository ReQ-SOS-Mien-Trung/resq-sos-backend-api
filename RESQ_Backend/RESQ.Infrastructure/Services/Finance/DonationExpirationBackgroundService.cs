using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RESQ.Application.UseCases.Finance.Commands.VerifyPayOSPayment;
using RESQ.Application.UseCases.Finance.Commands.VerifyZaloPayPayment;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Infrastructure.Services.Finance;

public class DonationExpirationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DonationExpirationBackgroundService> _logger;

    // Check every 1 minute
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
    
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

        // Stagger startup to avoid connection pool exhaustion when all services start simultaneously
        await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken);

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
        var donationPaymentProcessingService = scope.ServiceProvider.GetRequiredService<IDonationPaymentProcessingService>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var now = DateTime.UtcNow;

        var expiredDonations = await donationRepository.GetPendingDonationsPastDeadlineAsync(now, stoppingToken);

        if (expiredDonations.Any())
        {
            _logger.LogInformation("Found {Count} expired pending donations. Updating status to Failed...", expiredDonations.Count);

            foreach (var donation in expiredDonations)
            {
                if (await TryReconcilePendingDonationAsync(donation, mediator, stoppingToken))
                {
                    continue;
                }

                await donationPaymentProcessingService.TryProcessFailureAsync(
                    donation.Id,
                    "[System: Hết hạn thanh toán sau 15 phút]",
                    stoppingToken);
            }
        }
    }

    private async Task<bool> TryReconcilePendingDonationAsync(
        RESQ.Domain.Entities.Finance.DonationModel donation,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (donation.PaymentMethodCode == PaymentMethodCode.ZALOPAY && !string.IsNullOrWhiteSpace(donation.OrderId))
        {
            return await mediator.Send(
                new VerifyZaloPayPaymentCommand { AppTransId = donation.OrderId },
                cancellationToken);
        }

        if (donation.PaymentMethodCode == PaymentMethodCode.PAYOS && !string.IsNullOrWhiteSpace(donation.OrderId))
        {
            return await mediator.Send(
                new VerifyPayOSPaymentCommand { OrderId = donation.OrderId },
                cancellationToken);
        }

        return false;
    }
}

