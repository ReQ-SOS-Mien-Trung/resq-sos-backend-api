using Microsoft.AspNetCore.SignalR;
using RESQ.Application.Common.Interfaces;

namespace RESQ.Infrastructure.Notifications;

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;

    public NotificationService(IHubContext<NotificationHub, INotificationClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyAllAsync(string message)
    {
        await _hubContext.Clients.All.ReceiveNotification(message);
    }

    public async Task NotifyUserAsync(string userId, string message)
    {
        await _hubContext.Clients.User(userId).ReceiveNotification(message);
    }

    public async Task NotifyGroupAsync(string groupName, string message)
    {
        await _hubContext.Clients.Group(groupName).ReceiveNotification(message);
    }
}
