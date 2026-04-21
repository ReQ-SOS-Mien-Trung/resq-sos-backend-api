using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Logistics;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Infrastructure.Services.Logistics;

public class SupplyRequestDeadlineBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<SupplyRequestDeadlineBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private const string AutoRejectReason = "Há»‡ thá»‘ng tá»± Ä‘á»™ng tá»« chá»‘i do quÃ¡ thá»i gian pháº£n há»“i.";

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<SupplyRequestDeadlineBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Supply request deadline background service started.");

        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingSupplyRequestsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing supply request deadlines.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }

        _logger.LogInformation("Supply request deadline background service stopped.");
    }

    private async Task ProcessPendingSupplyRequestsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var configRepository = scope.ServiceProvider.GetRequiredService<ISupplyRequestPriorityConfigRepository>();
        var supplyRequestRepository = scope.ServiceProvider.GetRequiredService<ISupplyRequestRepository>();
        var firebaseService = scope.ServiceProvider.GetRequiredService<IFirebaseService>();
        var operationalHubService = scope.ServiceProvider.GetRequiredService<IOperationalHubService>();

        var config = await configRepository.GetAsync(cancellationToken);
        var timing = config == null
            ? SupplyRequestPriorityPolicy.DefaultTiming
            : new SupplyRequestPriorityTiming(config.UrgentMinutes, config.HighMinutes, config.MediumMinutes);

        var now = DateTime.UtcNow;
        var pendingRequests = await supplyRequestRepository.GetPendingForMonitoringAsync(cancellationToken);

        foreach (var request in pendingRequests)
        {
            var priority = request.PriorityLevel;

            var autoRejectAt = request.AutoRejectAt
                ?? SupplyRequestPriorityPolicy.ResolveAutoRejectAt(request.CreatedAt, priority, timing);

            if (!request.AutoRejectAt.HasValue)
                await supplyRequestRepository.SetAutoRejectAtAsync(request.Id, autoRejectAt, cancellationToken);

            if (now >= autoRejectAt)
            {
                var autoRejected = await supplyRequestRepository.AutoRejectIfPendingAsync(request.Id, AutoRejectReason, cancellationToken);
                if (!autoRejected)
                    continue;

                await firebaseService.SendNotificationToUserAsync(
                    request.RequestedBy,
                    "YÃªu cáº§u tiáº¿p táº¿ Ä‘Ã£ bá»‹ tá»± Ä‘á»™ng tá»« chá»‘i",
                    $"YÃªu cáº§u tiáº¿p táº¿ sá»‘ {request.Id} Ä‘Ã£ bá»‹ há»‡ thá»‘ng tá»± Ä‘á»™ng tá»« chá»‘i vÃ¬ quÃ¡ thá»i gian pháº£n há»“i tá»« kho nguá»“n.",
                    "supply_request_auto_rejected",
                    cancellationToken);

                var requestDetail = await supplyRequestRepository.GetByIdAsync(request.Id, cancellationToken);
                if (requestDetail != null)
                {
                    await operationalHubService.PushSupplyRequestUpdateAsync(
                        new SupplyRequestRealtimeUpdate
                        {
                            RequestId = requestDetail.Id,
                            RequestingDepotId = requestDetail.RequestingDepotId,
                            SourceDepotId = requestDetail.SourceDepotId,
                            Action = "AutoRejected",
                            SourceStatus = "Rejected",
                            RequestingStatus = "Rejected",
                            RejectedReason = AutoRejectReason
                        },
                        cancellationToken);
                }

                continue;
            }

            if (priority == SupplyRequestPriorityLevel.Medium && !request.HighEscalationNotified)
            {
                var highEscalationAt = SupplyRequestPriorityPolicy.ResolveHighEscalationAt(autoRejectAt, priority, timing);
                if (highEscalationAt.HasValue && now >= highEscalationAt.Value)
                {
                    var sourceManagerUserId = await supplyRequestRepository.GetActiveManagerUserIdByDepotIdAsync(request.SourceDepotId, cancellationToken);
                    if (sourceManagerUserId.HasValue)
                    {
                        await firebaseService.SendNotificationToUserAsync(
                            sourceManagerUserId.Value,
                            "YÃªu cáº§u tiáº¿p táº¿ Ä‘Ã£ vÃ o ngÆ°á»¡ng gáº¥p",
                            $"YÃªu cáº§u tiáº¿p táº¿ sá»‘ {request.Id} Ä‘Ã£ vÃ o ngÆ°á»¡ng gáº¥p. Vui lÃ²ng Æ°u tiÃªn kiá»ƒm tra vÃ  pháº£n há»“i.",
                            "supply_request_high_escalation",
                            cancellationToken);

                        await supplyRequestRepository.MarkHighEscalationNotifiedAsync(request.Id, cancellationToken);
                    }
                }
            }

            if (!request.UrgentEscalationNotified)
            {
                var urgentEscalationAt = SupplyRequestPriorityPolicy.ResolveUrgentEscalationAt(autoRejectAt, priority, timing);
                if (urgentEscalationAt.HasValue && now >= urgentEscalationAt.Value)
                {
                    var sourceManagerUserId = await supplyRequestRepository.GetActiveManagerUserIdByDepotIdAsync(request.SourceDepotId, cancellationToken);
                    if (sourceManagerUserId.HasValue)
                    {
                        await firebaseService.SendNotificationToUserAsync(
                            sourceManagerUserId.Value,
                            "YÃªu cáº§u tiáº¿p táº¿ Ä‘Ã£ vÃ o ngÆ°á»¡ng kháº©n cáº¥p",
                            $"YÃªu cáº§u tiáº¿p táº¿ sá»‘ {request.Id} Ä‘Ã£ vÃ o ngÆ°á»¡ng kháº©n cáº¥p. Vui lÃ²ng xá»­ lÃ½ ngay Ä‘á»ƒ trÃ¡nh tá»± Ä‘á»™ng tá»« chá»‘i.",
                            "supply_request_urgent_escalation",
                            cancellationToken);

                        await supplyRequestRepository.MarkUrgentEscalationNotifiedAsync(request.Id, cancellationToken);
                    }
                }
            }
        }
    }
}
