using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RESQ.Presentation.Hubs;

[Authorize]
public class AdminIdentityHub : Hub
{
    internal const string RescuerApplicationsGroup = "admin-identity:rescuer-applications";

    public Task SubscribeRescuerApplications() =>
        Groups.AddToGroupAsync(Context.ConnectionId, RescuerApplicationsGroup);

    public Task UnsubscribeRescuerApplications() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RescuerApplicationsGroup);

    public Task SubscribeRescuerApplication(int applicationId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, RescuerApplicationGroup(applicationId));

    public Task UnsubscribeRescuerApplication(int applicationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RescuerApplicationGroup(applicationId));

    internal static string RescuerApplicationGroup(int applicationId) => $"admin-identity:rescuer-application:{applicationId}";
}
