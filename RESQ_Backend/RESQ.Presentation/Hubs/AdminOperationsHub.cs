using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RESQ.Presentation.Hubs;

[Authorize]
public class AdminOperationsHub : Hub
{
    internal const string DepotsGroup = "admin-operations:depots";
    internal const string SOSClustersGroup = "admin-operations:sos-clusters";
    internal const string RescueTeamsGroup = "admin-operations:rescue-teams";

    public Task SubscribeDepots() =>
        Groups.AddToGroupAsync(Context.ConnectionId, DepotsGroup);

    public Task UnsubscribeDepots() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, DepotsGroup);

    public Task SubscribeDepot(int depotId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, DepotGroup(depotId));

    public Task UnsubscribeDepot(int depotId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, DepotGroup(depotId));

    public Task SubscribeDepotClosure(int closureId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, DepotClosureGroup(closureId));

    public Task UnsubscribeDepotClosure(int closureId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, DepotClosureGroup(closureId));

    public Task SubscribeTransfer(int transferId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, TransferGroup(transferId));

    public Task UnsubscribeTransfer(int transferId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, TransferGroup(transferId));

    public Task SubscribeSOSClusters() =>
        Groups.AddToGroupAsync(Context.ConnectionId, SOSClustersGroup);

    public Task UnsubscribeSOSClusters() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, SOSClustersGroup);

    public Task SubscribeSOSCluster(int clusterId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, SOSClusterGroup(clusterId));

    public Task UnsubscribeSOSCluster(int clusterId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, SOSClusterGroup(clusterId));

    public Task SubscribeMission(int missionId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, MissionGroup(missionId));

    public Task UnsubscribeMission(int missionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, MissionGroup(missionId));

    public Task SubscribeMissionActivities(int missionId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, MissionActivitiesGroup(missionId));

    public Task UnsubscribeMissionActivities(int missionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, MissionActivitiesGroup(missionId));

    public Task SubscribeRescueTeams() =>
        Groups.AddToGroupAsync(Context.ConnectionId, RescueTeamsGroup);

    public Task UnsubscribeRescueTeams() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RescueTeamsGroup);

    public Task SubscribeRescueTeam(int teamId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, RescueTeamGroup(teamId));

    public Task UnsubscribeRescueTeam(int teamId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RescueTeamGroup(teamId));

    internal static string DepotGroup(int depotId) => $"admin-operations:depot:{depotId}";
    internal static string DepotClosureGroup(int closureId) => $"admin-operations:closure:{closureId}";
    internal static string TransferGroup(int transferId) => $"admin-operations:transfer:{transferId}";
    internal static string SOSClusterGroup(int clusterId) => $"admin-operations:sos-cluster:{clusterId}";
    internal static string MissionGroup(int missionId) => $"admin-operations:mission:{missionId}";
    internal static string MissionActivitiesGroup(int missionId) => $"admin-operations:mission-activities:{missionId}";
    internal static string RescueTeamGroup(int teamId) => $"admin-operations:rescue-team:{teamId}";
}
