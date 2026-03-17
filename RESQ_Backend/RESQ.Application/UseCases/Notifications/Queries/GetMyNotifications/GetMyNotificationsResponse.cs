namespace RESQ.Application.UseCases.Notifications.Queries.GetMyNotifications;

public record NotificationItemDto(
    int UserNotificationId,
    int NotificationId,
    string? Title,
    string? Type,
    string? Content,
    bool IsRead,
    DateTime? ReadAt,
    DateTime? CreatedAt);

public record GetMyNotificationsResponse(
    IEnumerable<NotificationItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int UnreadCount);
