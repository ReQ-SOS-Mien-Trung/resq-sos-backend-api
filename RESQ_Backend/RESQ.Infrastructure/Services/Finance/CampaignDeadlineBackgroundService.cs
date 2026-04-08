using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Infrastructure.Services.Finance;

/// <summary>
/// Background service tự động đóng các chiến dịch đã quá ngày kết thúc (deadline).
/// Chạy mỗi 1 giờ một lần.
/// </summary>
public class CampaignDeadlineBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CampaignDeadlineBackgroundService> _logger;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    // GUID đại diện cho "hệ thống" — dùng làm modifiedBy khi auto-close
    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public CampaignDeadlineBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<CampaignDeadlineBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Campaign Deadline Background Service started.");

        // Stagger startup to avoid connection pool exhaustion when all services start simultaneously
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AutoCloseExpiredCampaignsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while auto-closing expired campaigns.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }

        _logger.LogInformation("Campaign Deadline Background Service stopped.");
    }

    private async Task AutoCloseExpiredCampaignsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var campaignRepo = scope.ServiceProvider.GetRequiredService<IFundCampaignRepository>();
        var unitOfWork   = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var expiredCampaigns = await campaignRepo.GetExpiredActiveAsync(stoppingToken);

        if (expiredCampaigns.Count == 0) return;

        _logger.LogInformation(
            "Found {Count} expired Active campaign(s). Auto-closing...", expiredCampaigns.Count);

        foreach (var campaign in expiredCampaigns)
        {
            try
            {
                campaign.Close(SystemUserId);
                await campaignRepo.UpdateAsync(campaign, stoppingToken);

                _logger.LogInformation(
                    "Campaign #{Id} '{Name}' auto-closed (deadline: {EndDate}).",
                    campaign.Id, campaign.Name, campaign.Duration?.EndDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to auto-close campaign #{Id}.", campaign.Id);
            }
        }

        await unitOfWork.SaveAsync();
    }
}
