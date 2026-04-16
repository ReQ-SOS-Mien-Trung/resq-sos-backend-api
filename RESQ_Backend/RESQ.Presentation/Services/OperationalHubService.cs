using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Services;
using RESQ.Presentation.Hubs;

namespace RESQ.Presentation.Services;

/// <summary>
/// Implementation của <see cref="IOperationalHubService"/>.
/// Tất cả push đều fire-and-forget: lỗi được log, không ném exception để không ảnh hưởng
/// luồng nghiệp vụ chính của caller.
/// </summary>
public sealed class OperationalHubService(
    IHubContext<OperationalHub> hubContext,
    ILogger<OperationalHubService> logger) : IOperationalHubService
{
    private readonly IHubContext<OperationalHub> _hubContext = hubContext;
    private readonly ILogger<OperationalHubService> _logger = logger;

    // ──────────────────────────────────────────────────────────────
    // Event name constants (dùng chung để tránh typo)
    // ──────────────────────────────────────────────────────────────
    private const string EventApListUpdate     = "ReceiveAssemblyPointListUpdate";
    private const string EventDepotInventory   = "ReceiveDepotInventoryUpdate";
    private const string EventLogisticsUpdate  = "ReceiveLogisticsUpdate";

    /// <inheritdoc/>
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
            _logger.LogWarning(ex,
                "[OperationalHub] Failed to push {Event}", EventApListUpdate);
        }
    }

    /// <inheritdoc/>
    public async Task PushDepotInventoryUpdateAsync(
        int depotId,
        string operation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new { depotId, operation, changedAt = DateTime.UtcNow };

            // Push đến tất cả client logistics (cần re-fetch by-cluster / alternative-depots)
            var taskAll = _hubContext.Clients
                .Group(OperationalHub.LogisticsGroup)
                .SendAsync(EventDepotInventory, payload, cancellationToken);

            // Push đến client đang xem trang inventory của kho này
            var taskDepot = _hubContext.Clients
                .Group(OperationalHub.DepotGroup(depotId))
                .SendAsync(EventDepotInventory, payload, cancellationToken);

            await Task.WhenAll(taskAll, taskDepot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[OperationalHub] Failed to push {Event} for DepotId={DepotId}",
                EventDepotInventory, depotId);
        }
    }

    /// <inheritdoc/>
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
                // Broadcast tới tất cả client logistics (clusterId = null → ảnh hưởng mọi cluster)
                _hubContext.Clients
                    .Group(OperationalHub.LogisticsGroup)
                    .SendAsync(EventLogisticsUpdate, payload, cancellationToken)
            };

            // Nếu biết clusterId cụ thể, push thêm đến group cluster đó
            if (clusterId.HasValue)
            {
                tasks.Add(_hubContext.Clients
                    .Group(OperationalHub.ClusterGroup(clusterId.Value))
                    .SendAsync(EventLogisticsUpdate, payload, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[OperationalHub] Failed to push {Event} resourceType={ResourceType} clusterId={ClusterId}",
                EventLogisticsUpdate, resourceType, clusterId);
        }
    }
}
