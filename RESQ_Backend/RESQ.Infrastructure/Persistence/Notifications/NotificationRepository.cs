using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Notifications;
using RESQ.Infrastructure.Entities.Notifications;
using RESQ.Infrastructure.Persistence.Base;

namespace RESQ.Infrastructure.Persistence.Notifications;

public class NotificationRepository(IUnitOfWork unitOfWork) : INotificationRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<int> CreateForUserAsync(Guid userId, string title, string type, string content, CancellationToken ct = default)
    {
        var notifRepo = _unitOfWork.GetRepository<Notification>();
        var userNotifRepo = _unitOfWork.GetRepository<UserNotification>();

        var notification = new Notification
        {
            Title = title,
            Type = type,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        await notifRepo.AddAsync(notification);
        await _unitOfWork.SaveAsync();

        var userNotification = new UserNotification
        {
            UserId = userId,
            NotificationId = notification.Id,
            IsRead = false,
            DeliveredAt = DateTime.UtcNow
        };

        await userNotifRepo.AddAsync(userNotification);
        await _unitOfWork.SaveAsync();

        return userNotification.Id;
    }

    public async Task<(IEnumerable<UserNotificationRecord> Items, int TotalCount)> GetPagedByUserIdAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _unitOfWork.GetRepository<UserNotification>()
            .AsQueryable(tracked: false)
            .Include(x => x.Notification)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Id);

        var totalCount = await query.CountAsync(ct);

        var pagedItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = pagedItems.Select(x => new UserNotificationRecord(
            x.Id,
            x.NotificationId ?? 0,
            x.Notification?.Title,
            x.Notification?.Type,
            x.Notification?.Content,
            x.IsRead ?? false,
            x.ReadAt,
            x.DeliveredAt,
            x.Notification?.CreatedAt));

        return (items, totalCount);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        var all = await _unitOfWork.GetRepository<UserNotification>()
            .GetAllByPropertyAsync(x => x.UserId == userId && x.IsRead != true);

        return all.Count;
    }

    public async Task<bool> MarkAsReadAsync(int userNotificationId, Guid userId, CancellationToken ct = default)
    {
        var entity = await _unitOfWork.GetRepository<UserNotification>()
            .GetByPropertyAsync(x => x.Id == userNotificationId && x.UserId == userId, tracked: true);

        if (entity is null) return false;

        entity.IsRead = true;
        entity.ReadAt = DateTime.UtcNow;

        await _unitOfWork.GetRepository<UserNotification>().UpdateAsync(entity);
        await _unitOfWork.SaveAsync();

        return true;
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        var unread = await _unitOfWork.GetRepository<UserNotification>()
            .GetAllByPropertyAsync(x => x.UserId == userId && x.IsRead != true);

        if (!unread.Any()) return;

        var now = DateTime.UtcNow;
        var repo = _unitOfWork.GetRepository<UserNotification>();

        foreach (var item in unread)
        {
            item.IsRead = true;
            item.ReadAt = now;
            await repo.UpdateAsync(item);
        }

        await _unitOfWork.SaveAsync();
    }
}
