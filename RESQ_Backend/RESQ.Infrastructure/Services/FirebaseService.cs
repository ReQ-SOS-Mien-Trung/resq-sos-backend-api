using FirebaseAdmin.Auth;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Services;

namespace RESQ.Infrastructure.Services;

public class FirebaseService(ILogger<FirebaseService> logger) : IFirebaseService
{
    private readonly ILogger<FirebaseService> _logger = logger;

    public async Task<FirebasePhoneTokenInfo> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var decoded = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken, cancellationToken);

            var phone = decoded.Claims.TryGetValue("phone_number", out var phoneObj)
                ? phoneObj?.ToString()
                : null;

            _logger.LogInformation("Firebase ID token verified. UID={uid}, Phone={phone}", decoded.Uid, phone);

            return new FirebasePhoneTokenInfo
            {
                Uid = decoded.Uid,
                Phone = phone
            };
        }
        catch (FirebaseAuthException ex)
        {
            _logger.LogWarning("Firebase token verification failed: {msg}", ex.Message);
            throw new UnauthorizedException("Firebase token không hợp lệ hoặc đã hết hạn");
        }
    }

    public async Task SendNotificationToUserAsync(Guid userId, string title, string body, CancellationToken cancellationToken = default)
    {
        try
        {
            // Định dạng topic: user_11111111111111111111111111111111 (sử dụng định dạng N để loại bỏ dấu gạch ngang)
            // Lưu ý: Ứng dụng client (iOS/Android/Web) sau khi login cần subscribe vào topic dạng "user_[UUID]" này.
            var topic = $"resq.user.{userId}";

            var message = new FirebaseAdmin.Messaging.Message()
            {
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = title,
                    Body = body
                },
                Topic = topic,
                Apns = new ApnsConfig 
                { 
                    Aps = new Aps { Sound = "default" } // Kích hoạt âm báo mặc định trên iOS
                }
            };

            var response = await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);
            _logger.LogInformation("Push notification sent to topic {Topic}. Response: {Response}", topic, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send push notification to user {UserId}", userId);
        }
    }

    public async Task SubscribeToUserTopicAsync(string fcmToken, Guid userId, CancellationToken cancellationToken = default)
    {
        var topic = $"resq.user.{userId}";
        try
        {
            var response = await FirebaseMessaging.DefaultInstance.SubscribeToTopicAsync(new[] { fcmToken }, topic);
            _logger.LogInformation("FCM token subscribed to topic {Topic}. Success={Success}, Fail={Fail}", topic, response.SuccessCount, response.FailureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe FCM token to topic {Topic}", topic);
        }
    }

    public async Task UnsubscribeFromUserTopicAsync(string fcmToken, Guid userId, CancellationToken cancellationToken = default)
    {
        var topic = $"resq.user.{userId}";
        try
        {
            var response = await FirebaseMessaging.DefaultInstance.UnsubscribeFromTopicAsync(new[] { fcmToken }, topic);
            _logger.LogInformation("FCM token unsubscribed from topic {Topic}. Success={Success}, Fail={Fail}", topic, response.SuccessCount, response.FailureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe FCM token from topic {Topic}", topic);
        }
    }
}
