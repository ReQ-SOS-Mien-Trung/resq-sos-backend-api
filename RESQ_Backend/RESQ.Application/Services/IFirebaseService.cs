namespace RESQ.Application.Services;

public class FirebasePhoneTokenInfo
{
    public string Uid { get; set; } = null!;
    public string? Phone { get; set; }
}

public interface IFirebaseService
{
    /// <summary>
    /// Verify a Firebase ID token obtained after phone OTP sign-in.
    /// Returns phone info when valid, throws UnauthorizedException otherwise.
    /// </summary>
    Task<FirebasePhoneTokenInfo> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi Push Notification (iOS, Android, Web) qua Firebase Cloud Messaging (FCM).
    /// </summary>
    Task SendNotificationToUserAsync(Guid userId, string title, string body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đăng ký FCM device token của trình duyệt web vào topic của user.
    /// Cần gọi sau khi login thành công trên web (Next.js).
    /// </summary>
    Task SubscribeToUserTopicAsync(string fcmToken, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hủy đăng ký FCM device token khỏi topic của user.
    /// Cần gọi khi logout trên web (Next.js).
    /// </summary>
    Task UnsubscribeFromUserTopicAsync(string fcmToken, Guid userId, CancellationToken cancellationToken = default);
}
