namespace RESQ.Application.Repositories.Notifications;

public record UserNotificationRecord(
    int UserNotificationId,
    int NotificationId,
    string? Title,
    string? Type,
    string? Content,
    bool IsRead,
    DateTime? ReadAt,
    DateTime? DeliveredAt,
    DateTime? CreatedAt);

public interface INotificationRepository
{
    /// <summary>Tạo notification + user_notification, trả về UserNotificationId.</summary>
    Task<int> CreateForUserAsync(Guid userId, string title, string type, string content, CancellationToken ct = default);

    /// <summary>Lấy danh sách notifications phân trang của user (mới nhất trước).</summary>
    Task<(IEnumerable<UserNotificationRecord> Items, int TotalCount)> GetPagedByUserIdAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Đếm số notifications chưa đọc.</summary>
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Đánh dấu một notification đã đọc. Trả về false nếu không tìm thấy hoặc không phải của user.</summary>
    Task<bool> MarkAsReadAsync(int userNotificationId, Guid userId, CancellationToken ct = default);

    /// <summary>Đánh dấu tất cả notifications của user là đã đọc.</summary>
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Kiểm tra user đã nhận notification cùng loại và tiêu đề kể từ một mốc thời gian hay chưa.</summary>
    Task<bool> HasRecentForUserAsync(
        Guid userId,
        string type,
        string title,
        DateTime sinceUtc,
        CancellationToken ct = default);
}
