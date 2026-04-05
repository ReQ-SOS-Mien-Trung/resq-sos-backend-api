using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Logistics;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Infrastructure.Services.Logistics;

public class SupplyRequestDeadlineBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<SupplyRequestDeadlineBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private const string AutoRejectReason = "Hệ thống tự động từ chối do quá thời gian phản hồi.";

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<SupplyRequestDeadlineBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Supply request deadline background service started.");

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
                    "Yêu cầu tiếp tế đã bị tự động từ chối",
                    $"Yêu cầu tiếp tế số {request.Id} đã bị hệ thống tự động từ chối vì quá thời gian phản hồi từ kho nguồn.",
                    "supply_request_auto_rejected",
                    cancellationToken);

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
                            "Yêu cầu tiếp tế đã vào ngưỡng gấp",
                            $"Yêu cầu tiếp tế số {request.Id} đã vào ngưỡng gấp. Vui lòng ưu tiên kiểm tra và phản hồi.",
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
                            "Yêu cầu tiếp tế đã vào ngưỡng khẩn cấp",
                            $"Yêu cầu tiếp tế số {request.Id} đã vào ngưỡng khẩn cấp. Vui lòng xử lý ngay để tránh tự động từ chối.",
                            "supply_request_urgent_escalation",
                            cancellationToken);

                        await supplyRequestRepository.MarkUrgentEscalationNotifiedAsync(request.Id, cancellationToken);
                    }
                }
            }
        }
    }
}
