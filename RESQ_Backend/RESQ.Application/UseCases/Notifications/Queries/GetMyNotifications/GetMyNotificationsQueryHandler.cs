using MediatR;
using RESQ.Application.Repositories.Notifications;

namespace RESQ.Application.UseCases.Notifications.Queries.GetMyNotifications;

public class GetMyNotificationsQueryHandler(INotificationRepository notificationRepository)
    : IRequestHandler<GetMyNotificationsQuery, GetMyNotificationsResponse>
{
    private readonly INotificationRepository _notificationRepository = notificationRepository;

    public async Task<GetMyNotificationsResponse> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _notificationRepository.GetPagedByUserIdAsync(
            request.UserId, request.Page, request.PageSize, cancellationToken);

        var unreadCount = await _notificationRepository.GetUnreadCountAsync(request.UserId, cancellationToken);

        var dtos = items.Select(x => new NotificationItemDto(
            x.UserNotificationId,
            x.NotificationId,
            x.Title,
            x.Type,
            x.Content,
            x.IsRead,
            x.ReadAt,
            x.CreatedAt));

        return new GetMyNotificationsResponse(dtos, totalCount, request.Page, request.PageSize, unreadCount);
    }
}
