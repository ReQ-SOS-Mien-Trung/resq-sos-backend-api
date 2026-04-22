using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Presentation.Hubs;

namespace RESQ.Presentation.Services;

public sealed class OperationalHubService(
    IHubContext<OperationalHub> hubContext,
    IAdminRealtimeHubService adminRealtimeHubService,
    ILogger<OperationalHubService> logger) : IOperationalHubService
{
    private readonly IHubContext<OperationalHub> _hubContext = hubContext;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
    private readonly ILogger<OperationalHubService> _logger = logger;

    private const string EventApListUpdate = "ReceiveAssemblyPointListUpdate";
    private const string EventDepotInventory = "ReceiveDepotInventoryUpdate";
    private const string EventLogisticsUpdate = "ReceiveLogisticsUpdate";
    private const string EventSupplyRequestUpdate = "ReceiveSupplyRequestUpdate";
    private const string EventDepotActivityUpdate = "ReceiveDepotActivityUpdate";
    private const string EventDepotClosureUpdate = "ReceiveDepotClosureUpdate";

    public async Task PushAssemblyPointListUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new { changedAt = DateTime.UtcNow };
            await _hubContext.Clients
                .Group(OperationalHub.AssemblyPointsGroup)
                .SendAsync(EventApListUpdate, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OperationalHub] Failed to push {Event}", EventApListUpdate);
        }
    }

    public async Task PushDepotInventoryUpdateAsync(
        int depotId,
        string operation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new { depotId, operation, changedAt = DateTime.UtcNow };

            var taskAll = _hubContext.Clients
                .Group(OperationalHub.LogisticsGroup)
                .SendAsync(EventDepotInventory, payload, cancellationToken);

            var taskDepot = _hubContext.Clients
                .Group(OperationalHub.DepotGroup(depotId))
                .SendAsync(EventDepotInventory, payload, cancellationToken);

            await Task.WhenAll(taskAll, taskDepot);

            await _adminRealtimeHubService.PushDepotUpdateAsync(
                new AdminDepotRealtimeUpdate
                {
                    EntityId = depotId,
                    EntityType = "Depot",
                    DepotId = depotId,
                    Action = operation,
                    Status = null,
                    ChangedAt = DateTime.UtcNow
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[OperationalHub] Failed to push {Event} for DepotId={DepotId}",
                EventDepotInventory,
                depotId);
        }
    }

    public async Task PushLogisticsUpdateAsync(
        string resourceType,
        int? clusterId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new { resourceType, clusterId, changedAt = DateTime.UtcNow };
            var tasks = new List<Task>
            {
                _hubContext.Clients
                    .Group(OperationalHub.LogisticsGroup)
                    .SendAsync(EventLogisticsUpdate, payload, cancellationToken)
            };

            if (clusterId.HasValue)
            {
                tasks.Add(_hubContext.Clients
                    .Group(OperationalHub.ClusterGroup(clusterId.Value))
                    .SendAsync(EventLogisticsUpdate, payload, cancellationToken));
            }

            await Task.WhenAll(tasks);

            switch (resourceType)
            {
                case "depots":
                    await _adminRealtimeHubService.PushDepotUpdateAsync(
                        new AdminDepotRealtimeUpdate
                        {
                            EntityId = null,
                            EntityType = "Depot",
                            DepotId = null,
                            Action = "ListChanged",
                            Status = null,
                            ChangedAt = DateTime.UtcNow
                        },
                        cancellationToken);
                    break;
                case "rescue-teams":
                    await _adminRealtimeHubService.PushRescueTeamUpdateAsync(
                        new AdminRescueTeamRealtimeUpdate
                        {
                            EntityId = null,
                            EntityType = "RescueTeam",
                            TeamId = null,
                            Action = "ListChanged",
                            Status = null,
                            ChangedAt = DateTime.UtcNow
                        },
                        cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[OperationalHub] Failed to push {Event} resourceType={ResourceType} clusterId={ClusterId}",
                EventLogisticsUpdate,
                resourceType,
                clusterId);
        }
    }

    public async Task PushSupplyRequestUpdateAsync(
        SupplyRequestRealtimeUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            update.ChangedAt = NormalizeChangedAt(update.ChangedAt);

            var groups = new HashSet<string>(StringComparer.Ordinal)
            {
                OperationalHub.SupplyRequestsDepotGroup(update.RequestingDepotId),
                OperationalHub.SupplyRequestsDepotGroup(update.SourceDepotId),
                OperationalHub.SupplyRequestGroup(update.RequestId)
            };

            await SendToGroupsAsync(groups, EventSupplyRequestUpdate, update, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[OperationalHub] Failed to push {Event} for SupplyRequestId={RequestId}",
                EventSupplyRequestUpdate,
                update.RequestId);
        }
    }

    public async Task PushDepotActivityUpdateAsync(
        DepotActivityRealtimeUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            update.ChangedAt = NormalizeChangedAt(update.ChangedAt);

            var groups = new HashSet<string>(StringComparer.Ordinal)
            {
                OperationalHub.DepotActivitiesGroup(update.DepotId),
                OperationalHub.ActivityGroup(update.ActivityId)
            };

            await SendToGroupsAsync(groups, EventDepotActivityUpdate, update, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[OperationalHub] Failed to push {Event} for ActivityId={ActivityId}",
                EventDepotActivityUpdate,
                update.ActivityId);
        }
    }

    public async Task PushDepotClosureUpdateAsync(
        DepotClosureRealtimeUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            update.ChangedAt = NormalizeChangedAt(update.ChangedAt);

            var groups = new HashSet<string>(StringComparer.Ordinal)
            {
                OperationalHub.DepotClosuresGroup(update.SourceDepotId)
            };

            if (update.TargetDepotId.HasValue)
                groups.Add(OperationalHub.DepotClosuresGroup(update.TargetDepotId.Value));

            if (update.ClosureId.HasValue)
                groups.Add(OperationalHub.ClosureGroup(update.ClosureId.Value));

            if (update.TransferId.HasValue)
                groups.Add(OperationalHub.TransferGroup(update.TransferId.Value));

            await SendToGroupsAsync(groups, EventDepotClosureUpdate, update, cancellationToken);

            if (string.Equals(update.EntityType, "Transfer", StringComparison.OrdinalIgnoreCase)
                && update.TransferId.HasValue
                && update.TargetDepotId.HasValue)
            {
                await _adminRealtimeHubService.PushTransferUpdateAsync(
                    new AdminTransferRealtimeUpdate
                    {
                        EntityId = update.TransferId,
                        EntityType = "Transfer",
                        TransferId = update.TransferId.Value,
                        ClosureId = update.ClosureId,
                        SourceDepotId = update.SourceDepotId,
                        TargetDepotId = update.TargetDepotId.Value,
                        Action = update.Action,
                        Status = update.Status,
                        ChangedAt = update.ChangedAt
                    },
                    cancellationToken);
            }

            if (string.Equals(update.EntityType, "Closure", StringComparison.OrdinalIgnoreCase))
            {
                await _adminRealtimeHubService.PushDepotClosureUpdateAsync(
                    new AdminDepotClosureRealtimeUpdate
                    {
                        EntityId = update.ClosureId,
                        EntityType = "DepotClosure",
                        ClosureId = update.ClosureId,
                        SourceDepotId = update.SourceDepotId,
                        TargetDepotId = update.TargetDepotId,
                        Action = update.Action,
                        Status = update.Status,
                        ChangedAt = update.ChangedAt
                    },
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[OperationalHub] Failed to push {Event} for ClosureId={ClosureId} TransferId={TransferId}",
                EventDepotClosureUpdate,
                update.ClosureId,
                update.TransferId);
        }
    }

    private async Task SendToGroupsAsync(
        IEnumerable<string> groups,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        var tasks = groups
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => _hubContext.Clients.Group(group).SendAsync(eventName, payload, cancellationToken))
            .ToList();

        if (tasks.Count == 0)
            return;

        await Task.WhenAll(tasks);
    }

    private static DateTime NormalizeChangedAt(DateTime changedAt) =>
        changedAt == default ? DateTime.UtcNow : changedAt;
}
