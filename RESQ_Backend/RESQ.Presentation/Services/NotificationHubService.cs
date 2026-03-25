using Microsoft.AspNetCore.SignalR;
using RESQ.Application.Services;
using RESQ.Presentation.Hubs;

namespace RESQ.Presentation.Services;

public class NotificationHubService(IHubContext<NotificationHub> hubContext) : INotificationHubService
{
    private readonly IHubContext<NotificationHub> _hubContext = hubContext;

    public async Task SendToUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken = default)
    {
        var group = $"notification_user_{userId}";
        await _hubContext.Clients.Group(group).SendAsync(eventName, payload, cancellationToken);
    }

    public async Task SendToGroupAsync(string groupName, string eventName, object payload, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(groupName).SendAsync(eventName, payload, cancellationToken);
    }
}
