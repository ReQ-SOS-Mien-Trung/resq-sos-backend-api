using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RESQ.Presentation.Hubs;

/// <summary>
/// Hub SignalR cho dữ liệu vận hành real-time.
/// Endpoint kết nối: /hubs/operational?access_token={jwt}
///
/// Groups client tự động tham gia khi kết nối:
///   - "operational:assembly-points" → nhận ReceiveAssemblyPointListUpdate
///   - "operational:logistics"       → nhận ReceiveDepotInventoryUpdate, ReceiveLogisticsUpdate
///
/// Groups client subscribe thủ công qua client methods:
///   - "operational:depot:{depotId}"    → nhận ReceiveDepotInventoryUpdate cho kho cụ thể
///   - "operational:cluster:{clusterId}" → nhận ReceiveLogisticsUpdate cho cluster cụ thể
///
/// Events từ server → client:
///   "ReceiveAssemblyPointListUpdate"  { changedAt }
///   "ReceiveDepotInventoryUpdate"     { depotId, operation, changedAt }
///   "ReceiveLogisticsUpdate"          { resourceType, clusterId, changedAt }
/// </summary>
[Authorize]
public class OperationalHub : Hub
{
    internal const string AssemblyPointsGroup = "operational:assembly-points";
    internal const string LogisticsGroup = "operational:logistics";

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        // Tất cả client đều nhận được AP list update và logistics update tổng
        await Groups.AddToGroupAsync(Context.ConnectionId, AssemblyPointsGroup);
        await Groups.AddToGroupAsync(Context.ConnectionId, LogisticsGroup);
        await base.OnConnectedAsync();
    }

    /// <inheritdoc/>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AssemblyPointsGroup);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, LogisticsGroup);
        await base.OnDisconnectedAsync(exception);
    }

    // ──────────────────────────────────────────────────────────────
    // Client-callable methods (subscribe / unsubscribe per resource)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Client đăng ký nhận cập nhật tồn kho cho một kho cụ thể.
    /// Gọi khi mở trang /logistics/inventory/depot/{depotId}.
    /// </summary>
    public Task SubscribeDepot(int depotId)
        => Groups.AddToGroupAsync(Context.ConnectionId, DepotGroup(depotId));

    /// <summary>Huỷ đăng ký kho. Gọi khi rời trang hoặc đổi kho.</summary>
    public Task UnsubscribeDepot(int depotId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, DepotGroup(depotId));

    /// <summary>
    /// Client đăng ký nhận cập nhật dữ liệu logistics cho một cluster SOS cụ thể.
    /// Gọi khi mở trang quản lý cluster (rescue-teams/by-cluster, depot/by-cluster, alternative-depots).
    /// </summary>
    public Task SubscribeCluster(int clusterId)
        => Groups.AddToGroupAsync(Context.ConnectionId, ClusterGroup(clusterId));

    /// <summary>Huỷ đăng ký cluster.</summary>
    public Task UnsubscribeCluster(int clusterId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, ClusterGroup(clusterId));

    // ──────────────────────────────────────────────────────────────
    // Group name helpers (kept internal so OperationalHubService reuses them)
    // ──────────────────────────────────────────────────────────────
    internal static string DepotGroup(int depotId) => $"operational:depot:{depotId}";
    internal static string ClusterGroup(int clusterId) => $"operational:cluster:{clusterId}";
}
