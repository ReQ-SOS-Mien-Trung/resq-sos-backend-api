using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RESQ.Application.Common.Models;

namespace RESQ.Presentation.Hubs;

/// <summary>
/// Real-time notification hub.
/// Mỗi user tự động join group "notification_user_{userId}" khi kết nối.
/// Server push event "ReceiveNotification" khi có notification mới.
/// Client kết nối với JWT Bearer token qua query string ?access_token=...
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(userId));

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(userId));

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join group realtime theo mission + depot để nhận DepotUpdated event.
    /// </summary>
    public Task JoinDepotGroup(int? missionId, int depotId)
    {
        var group = DepotRealtimeGroupKey.Build(missionId, depotId);
        return Groups.AddToGroupAsync(Context.ConnectionId, group);
    }

    /// <summary>
    /// Leave group realtime theo mission + depot.
    /// </summary>
    public Task LeaveDepotGroup(int? missionId, int depotId)
    {
        var group = DepotRealtimeGroupKey.Build(missionId, depotId);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
    }

    private static string GetGroupName(string userId) => $"notification_user_{userId}";
}
