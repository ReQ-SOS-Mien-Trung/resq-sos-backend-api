using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RESQ.Presentation.Hubs;

[Authorize]
public class AdminSystemHub : Hub
{
    internal const string SystemConfigsGroup = "admin-system:system-configs";
    internal const string AiConfigsGroup = "admin-system:ai-configs";

    public Task SubscribeSystemConfigs() =>
        Groups.AddToGroupAsync(Context.ConnectionId, SystemConfigsGroup);

    public Task UnsubscribeSystemConfigs() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, SystemConfigsGroup);

    public Task SubscribeAiConfigs() =>
        Groups.AddToGroupAsync(Context.ConnectionId, AiConfigsGroup);

    public Task UnsubscribeAiConfigs() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, AiConfigsGroup);
}
