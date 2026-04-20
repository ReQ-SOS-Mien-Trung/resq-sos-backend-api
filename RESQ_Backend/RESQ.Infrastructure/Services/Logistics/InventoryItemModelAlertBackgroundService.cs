using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Notifications;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Queries.GetMyDepotItemModelAlerts;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Services.Logistics;

public class InventoryItemModelAlertBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<InventoryItemModelAlertBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
    private static readonly HashSet<string> IgnoredDepotStatuses = new()
    {
        nameof(DepotStatus.Created),
        nameof(DepotStatus.PendingAssignment),
        nameof(DepotStatus.Closed),
        nameof(DepotStatus.Closing)
    };

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<InventoryItemModelAlertBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Inventory item model alert background service started.");

        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAlertsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing inventory item model alerts.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }

        _logger.LogInformation("Inventory item model alert background service stopped.");
    }

    private async Task ProcessAlertsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var depotInventoryRepository = scope.ServiceProvider.GetRequiredService<IDepotInventoryRepository>();
        var notificationRepository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var firebaseService = scope.ServiceProvider.GetRequiredService<IFirebaseService>();

        var assignments = await (
            from depotManager in unitOfWork.Set<DepotManager>()
            join depot in unitOfWork.Set<Depot>() on depotManager.DepotId equals depot.Id
            where depotManager.UserId.HasValue
               && depotManager.DepotId.HasValue
               && depotManager.UnassignedAt == null
               && !IgnoredDepotStatuses.Contains(depot.Status)
            select new DepotAssignment(
                depotManager.UserId!.Value,
                depot.Id,
                depot.Name ?? string.Empty))
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
            return;

        var todayUtcDate = DateTime.UtcNow.Date;
        var depotGroups = assignments
            .GroupBy(x => new { x.DepotId, x.DepotName })
            .ToList();

        foreach (var depotGroup in depotGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var expiringCandidates = await depotInventoryRepository.GetExpiringItemModelAlertCandidatesAsync(
                depotGroup.Key.DepotId,
                cancellationToken);
            var maintenanceCandidates = await depotInventoryRepository.GetMaintenanceItemModelAlertCandidatesAsync(
                depotGroup.Key.DepotId,
                cancellationToken);

            var alerts = DepotItemModelAlertFactory.BuildAll(
                expiringCandidates,
                maintenanceCandidates,
                todayUtcDate);

            if (alerts.Count == 0)
                continue;

            var expiringAlerts = alerts
                .Where(x => string.Equals(x.AlertType, "ExpiringSoon", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var maintenanceAlerts = alerts
                .Where(x => string.Equals(x.AlertType, "MaintenanceDue", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var managerUserIds = depotGroup
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

            foreach (var managerUserId in managerUserIds)
            {
                if (expiringAlerts.Count > 0)
                {
                    await SendNotificationIfNeededAsync(
                        notificationRepository,
                        firebaseService,
                        managerUserId,
                        depotGroup.Key.DepotId,
                        depotGroup.Key.DepotName,
                        "inventory_expiring_alert",
                        "Cảnh báo vật phẩm sắp hết hạn",
                        "ExpiringSoon",
                        expiringAlerts,
                        todayUtcDate,
                        cancellationToken);
                }

                if (maintenanceAlerts.Count > 0)
                {
                    await SendNotificationIfNeededAsync(
                        notificationRepository,
                        firebaseService,
                        managerUserId,
                        depotGroup.Key.DepotId,
                        depotGroup.Key.DepotName,
                        "inventory_maintenance_alert",
                        "Cảnh báo vật phẩm đến kỳ bảo trì",
                        "MaintenanceDue",
                        maintenanceAlerts,
                        todayUtcDate,
                        cancellationToken);
                }
            }
        }
    }

    private async Task SendNotificationIfNeededAsync(
        INotificationRepository notificationRepository,
        IFirebaseService firebaseService,
        Guid userId,
        int depotId,
        string depotName,
        string notificationType,
        string titlePrefix,
        string alertType,
        IReadOnlyCollection<DepotItemModelAlertDto> alerts,
        DateTime sinceUtc,
        CancellationToken cancellationToken)
    {
        var title = string.IsNullOrWhiteSpace(depotName)
            ? $"{titlePrefix} - Kho #{depotId}"
            : $"{titlePrefix} - {depotName} (#{depotId})";

        var alreadySent = await notificationRepository.HasRecentForUserAsync(
            userId,
            notificationType,
            title,
            sinceUtc,
            cancellationToken);

        if (alreadySent)
            return;

        var body = BuildNotificationBody(depotId, depotName, alertType, alerts);
        var data = new Dictionary<string, string>
        {
            ["depotId"] = depotId.ToString(),
            ["alertType"] = alertType,
            ["screen"] = "inventory_item_model_alerts"
        };

        await firebaseService.SendNotificationToUserAsync(
            userId,
            title,
            body,
            notificationType,
            data,
            cancellationToken);
    }

    private static string BuildNotificationBody(
        int depotId,
        string depotName,
        string alertType,
        IReadOnlyCollection<DepotItemModelAlertDto> alerts)
    {
        var depotLabel = string.IsNullOrWhiteSpace(depotName)
            ? $"kho #{depotId}"
            : $"kho {depotName}";

        var topItemNames = alerts
            .Select(x => x.ItemModelName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var preview = topItemNames.Count == 0
            ? "các vật phẩm trong kho"
            : string.Join(", ", topItemNames);

        var totalDistinctItems = alerts
            .Select(x => x.ItemModelName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var suffix = totalDistinctItems > topItemNames.Count ? " và các vật phẩm khác" : string.Empty;

        if (string.Equals(alertType, "ExpiringSoon", StringComparison.OrdinalIgnoreCase))
        {
            var minDueInDays = alerts.Min(x => x.DueInDays);
            return minDueInDays < 0
                ? $"Có {alerts.Count} nhóm vật phẩm tại {depotLabel} đã chạm hoặc quá hạn: {preview}{suffix}. Vui lòng kiểm tra ngay."
                : $"Có {alerts.Count} nhóm vật phẩm tại {depotLabel} sắp hết hạn: {preview}{suffix}. Mốc gần nhất còn {Math.Max(minDueInDays, 0)} ngày.";
        }

        var actionableQuantity = alerts.Sum(x => x.ActionableQuantity);
        return actionableQuantity > 0
            ? $"Có {alerts.Count} nhóm vật phẩm tại {depotLabel} đã đến kỳ bảo trì: {preview}{suffix}. Hiện có {actionableQuantity} thiết bị có thể xử lý ngay."
            : $"Có {alerts.Count} nhóm vật phẩm tại {depotLabel} đã đến kỳ bảo trì: {preview}{suffix}. Hiện chưa có thiết bị sẵn sàng để xử lý ngay.";
    }

    private sealed record DepotAssignment(Guid UserId, int DepotId, string DepotName);
}
