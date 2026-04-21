using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RESQ.Presentation.Hubs;

[Authorize]
public class OperationalHub : Hub
{
    internal const string AssemblyPointsGroup = "operational:assembly-points";
    internal const string LogisticsGroup = "operational:logistics";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AssemblyPointsGroup);
        await Groups.AddToGroupAsync(Context.ConnectionId, LogisticsGroup);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AssemblyPointsGroup);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, LogisticsGroup);
        await base.OnDisconnectedAsync(exception);
    }

    public Task SubscribeDepot(int depotId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, DepotGroup(depotId));

    public Task UnsubscribeDepot(int depotId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, DepotGroup(depotId));

    public Task SubscribeCluster(int clusterId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, ClusterGroup(clusterId));

    public Task UnsubscribeCluster(int clusterId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ClusterGroup(clusterId));

    public Task SubscribeSupplyRequests(int depotId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, SupplyRequestsDepotGroup(depotId));

    public Task UnsubscribeSupplyRequests(int depotId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, SupplyRequestsDepotGroup(depotId));

    public Task SubscribeSupplyRequest(int requestId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, SupplyRequestGroup(requestId));

    public Task UnsubscribeSupplyRequest(int requestId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, SupplyRequestGroup(requestId));

    public Task SubscribeDepotActivities(int depotId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, DepotActivitiesGroup(depotId));

    public Task UnsubscribeDepotActivities(int depotId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, DepotActivitiesGroup(depotId));

    public Task SubscribeActivity(int activityId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, ActivityGroup(activityId));

    public Task UnsubscribeActivity(int activityId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ActivityGroup(activityId));

    public Task SubscribeDepotClosures(int depotId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, DepotClosuresGroup(depotId));

    public Task UnsubscribeDepotClosures(int depotId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, DepotClosuresGroup(depotId));

    public Task SubscribeClosure(int closureId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, ClosureGroup(closureId));

    public Task UnsubscribeClosure(int closureId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ClosureGroup(closureId));

    public Task SubscribeTransfer(int transferId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, TransferGroup(transferId));

    public Task UnsubscribeTransfer(int transferId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, TransferGroup(transferId));

    internal static string DepotGroup(int depotId) => $"operational:depot:{depotId}";
    internal static string ClusterGroup(int clusterId) => $"operational:cluster:{clusterId}";
    internal static string SupplyRequestsDepotGroup(int depotId) => $"operational:supply-requests:depot:{depotId}";
    internal static string SupplyRequestGroup(int requestId) => $"operational:supply-request:{requestId}";
    internal static string DepotActivitiesGroup(int depotId) => $"operational:activities:depot:{depotId}";
    internal static string ActivityGroup(int activityId) => $"operational:activity:{activityId}";
    internal static string DepotClosuresGroup(int depotId) => $"operational:closures:depot:{depotId}";
    internal static string ClosureGroup(int closureId) => $"operational:closure:{closureId}";
    internal static string TransferGroup(int transferId) => $"operational:transfer:{transferId}";
}
