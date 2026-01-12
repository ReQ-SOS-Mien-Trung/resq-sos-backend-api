namespace RESQ.Application.Common.Interfaces;

public interface INotificationClient
{
    Task ReceiveNotification(string message);
    Task ReceiveSystemUpdate(object data);
}