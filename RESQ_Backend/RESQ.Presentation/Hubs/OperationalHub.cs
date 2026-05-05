using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RESQ.Presentation.Hubs;

[Authorize]
public class OperationalHub : Hub
{
    internal const string AssemblyPointsGroup = "operational:assembly-points";
    internal const string LogisticsGroup = "operational:logistics";
    internal const string DepotFundsGroup = "operational:depot-funds";

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

    public Task SubscribeInventoryLots(int depotId, int itemModelId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, InventoryLotsGroup(depotId, itemModelId));

    public Task UnsubscribeInventoryLots(int depotId, int itemModelId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, InventoryLotsGroup(depotId, itemModelId));

    public Task SubscribeDepotCharts(int depotId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, DepotChartsGroup(depotId));

    public Task UnsubscribeDepotCharts(int depotId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, DepotChartsGroup(depotId));

    public Task SubscribeDepotFunds() =>
        Groups.AddToGroupAsync(Context.ConnectionId, DepotFundsGroup);

    public Task UnsubscribeDepotFunds() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, DepotFundsGroup);

    public Task SubscribeDepotFund(int depotId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, DepotFundGroup(depotId));

    public Task UnsubscribeDepotFund(int depotId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, DepotFundGroup(depotId));

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

    public Task SubscribeUpcomingReturns(int depotId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, UpcomingReturnsDepotGroup(depotId));

    public Task UnsubscribeUpcomingReturns(int depotId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, UpcomingReturnsDepotGroup(depotId));

    public Task SubscribeActivity(int activityId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, ActivityGroup(activityId));

    public Task UnsubscribeActivity(int activityId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ActivityGroup(activityId));

    public Task SubscribeAssemblyEventCheckedInRescuers(int eventId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, AssemblyEventCheckedInRescuersGroup(eventId));

    public Task UnsubscribeAssemblyEventCheckedInRescuers(int eventId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, AssemblyEventCheckedInRescuersGroup(eventId));

    public Task SubscribeAssemblyPointCheckedInRescuers(int assemblyPointId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, AssemblyPointCheckedInRescuersGroup(assemblyPointId));

    public Task UnsubscribeAssemblyPointCheckedInRescuers(int assemblyPointId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, AssemblyPointCheckedInRescuersGroup(assemblyPointId));

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
    internal static string InventoryLotsGroup(int depotId, int itemModelId) => $"operational:inventory-lots:depot:{depotId}:item:{itemModelId}";
    internal static string DepotChartsGroup(int depotId) => $"operational:depot-charts:{depotId}";
    internal static string DepotFundGroup(int depotId) => $"operational:depot-funds:{depotId}";
    internal static string ClusterGroup(int clusterId) => $"operational:cluster:{clusterId}";
    internal static string SupplyRequestsDepotGroup(int depotId) => $"operational:supply-requests:depot:{depotId}";
    internal static string SupplyRequestGroup(int requestId) => $"operational:supply-request:{requestId}";
    internal static string DepotActivitiesGroup(int depotId) => $"operational:activities:depot:{depotId}";
    internal static string UpcomingReturnsDepotGroup(int depotId) => $"operational:upcoming-returns:depot:{depotId}";
    internal static string ActivityGroup(int activityId) => $"operational:activity:{activityId}";
    internal static string AssemblyEventCheckedInRescuersGroup(int eventId) => $"operational:assembly-event:{eventId}:checked-in-rescuers";
    internal static string AssemblyPointCheckedInRescuersGroup(int assemblyPointId) => $"operational:assembly-point:{assemblyPointId}:checked-in-rescuers";
    internal static string DepotClosuresGroup(int depotId) => $"operational:closures:depot:{depotId}";
    internal static string ClosureGroup(int closureId) => $"operational:closure:{closureId}";
    internal static string TransferGroup(int transferId) => $"operational:transfer:{transferId}";
}
