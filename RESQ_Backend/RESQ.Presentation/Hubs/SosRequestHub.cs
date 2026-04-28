using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RESQ.Application.Common.Constants;

namespace RESQ.Presentation.Hubs;

[Authorize(Policy = PermissionConstants.SosRequestView)]
public class SosRequestHub : Hub
{
    internal const string AllGroup = "sos-requests:all";
    internal const string UnclusteredGroup = "sos-requests:unclustered";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AllGroup);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AllGroup);
        await base.OnDisconnectedAsync(exception);
    }

    public Task SubscribeSosRequest(int id) =>
        Groups.AddToGroupAsync(Context.ConnectionId, RequestGroup(id));

    public Task UnsubscribeSosRequest(int id) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RequestGroup(id));

    public Task SubscribeSosCluster(int clusterId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, ClusterGroup(clusterId));

    public Task UnsubscribeSosCluster(int clusterId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ClusterGroup(clusterId));

    public Task SubscribeUnclusteredSosRequests() =>
        Groups.AddToGroupAsync(Context.ConnectionId, UnclusteredGroup);

    public Task UnsubscribeUnclusteredSosRequests() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, UnclusteredGroup);

    internal static string RequestGroup(int requestId) => $"sos-requests:request:{requestId}";
    internal static string ClusterGroup(int clusterId) => $"sos-requests:cluster:{clusterId}";
}
