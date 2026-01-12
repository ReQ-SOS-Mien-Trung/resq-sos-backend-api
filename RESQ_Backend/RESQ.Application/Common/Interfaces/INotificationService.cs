namespace RESQ.Application.Common.Interfaces;

public interface INotificationService
{
    Task NotifyAllAsync(string message);
    Task NotifyUserAsync(string userId, string message);
    Task NotifyGroupAsync(string groupName, string message);
}