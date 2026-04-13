namespace RESQ.Application.Services;

public class FirebasePhoneTokenInfo
{
    public string Uid { get; set; } = null!;
    public string? Phone { get; set; }
}

public class FirebaseGoogleUserInfo
{
    public string Uid { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Name { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
}

public interface IFirebaseService
{
    /// <summary>
    /// Verify a Firebase ID token obtained after phone OTP sign-in.
    /// Returns phone info when valid, throws UnauthorizedException otherwise.
    /// </summary>
    Task<FirebasePhoneTokenInfo> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify a Firebase ID token obtained after Google Sign-In.
    /// Returns Google user info (email, name) when valid, throws UnauthorizedException otherwise.
    /// </summary>
    Task<FirebaseGoogleUserInfo> VerifyGoogleIdTokenAsync(string idToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi Push Notification (iOS, Android, Web) qua FCM, lưu vào DB và đẩy real-time qua SignalR.
    /// </summary>
    Task SendNotificationToUserAsync(Guid userId, string title, string body, string type = "general", CancellationToken cancellationToken = default);

    /// <summary>
    /// Overload gửi notification kèm data dictionary - dùng cho các thông báo cần deep-link (VD: closureId, transferId).
    /// Dữ liệu trong <paramref name="data"/> được đính kèm vào FCM data payload để mobile app tự navigate.
    /// </summary>
    Task SendNotificationToUserAsync(Guid userId, string title, string body, string type, Dictionary<string, string> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi Push Notification đến toàn bộ user subscribe topic (dùng cho broadcast).
    /// Không lưu DB và không đẩy SignalR - chỉ FCM topic send.
    /// </summary>
    Task SendToTopicAsync(string topic, string title, string body, string type = "general", CancellationToken cancellationToken = default);

    /// <summary>
    /// Overload gửi broadcast với data dictionary tuỳ chỉnh (dùng cho alert có payload phức tạp).
    /// </summary>
    Task SendToTopicAsync(string topic, string title, string body, Dictionary<string, string> data, CancellationToken cancellationToken = default);

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
