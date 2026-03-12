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
}
