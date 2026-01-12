using Microsoft.AspNetCore.SignalR;
using RESQ.Application.Common.Interfaces;

namespace RESQ.Infrastructure.Notifications;

public class NotificationHub : Hub<INotificationClient>
{
    // Hub này giờ nằm ở Infrastructure để NotificationService có thể thấy nó
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}
