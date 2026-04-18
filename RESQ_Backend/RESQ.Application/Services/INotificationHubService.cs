namespace RESQ.Application.Services;

public interface INotificationHubService
{
    /// <summary>Đẩy real-time event đến user (qua SignalR NotificationHub group).</summary>
    Task SendToUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken = default);

    /// <summary>Đẩy real-time event đến một group SignalR bất kỳ.</summary>
    Task SendToGroupAsync(string groupName, string eventName, object payload, CancellationToken cancellationToken = default);
}
