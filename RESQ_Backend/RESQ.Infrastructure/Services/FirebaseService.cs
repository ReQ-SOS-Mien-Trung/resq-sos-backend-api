using FirebaseAdmin.Auth;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Notifications;
using RESQ.Application.Services;

namespace RESQ.Infrastructure.Services;

public class FirebaseService(
    ILogger<FirebaseService> logger,
    INotificationRepository notificationRepository,
    INotificationHubService notificationHubService) : IFirebaseService
{
    private readonly ILogger<FirebaseService> _logger = logger;
    private readonly INotificationRepository _notificationRepository = notificationRepository;
    private readonly INotificationHubService _notificationHubService = notificationHubService;

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

    public async Task SendNotificationToUserAsync(Guid userId, string title, string body, string type = "general", CancellationToken cancellationToken = default)
    {
        // 1. Persist to DB
        int userNotificationId = 0;
        try
        {
            userNotificationId = await _notificationRepository.CreateForUserAsync(userId, title, type, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist notification to DB for user {UserId}", userId);
        }

        // 2. Real-time push via SignalR
        try
        {
            await _notificationHubService.SendToUserAsync(userId, "ReceiveNotification", new
            {
                userNotificationId,
                title,
                type,
                body,
                isRead = false,
                createdAt = DateTime.UtcNow
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send SignalR notification to user {UserId}", userId);
        }

        // 3. FCM push notification
        try
        {
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
                    Aps = new Aps { Sound = "default" }
                }
            };

            var response = await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);
            _logger.LogInformation("Push notification sent to topic {Topic}. Response: {Response}", topic, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send FCM push notification to user {UserId}", userId);
        }
    }

    public async Task SendToTopicAsync(string topic, string title, string body, string type = "general", CancellationToken cancellationToken = default)
    {
        try
        {
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
                    Aps = new Aps { Sound = "default" }
                },
                Data = new Dictionary<string, string> { ["type"] = type }
            };

            var response = await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);
            _logger.LogInformation("FCM topic message sent to '{Topic}'. Response: {Response}", topic, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send FCM message to topic '{Topic}'", topic);
            throw;
        }
    }

    public async Task SubscribeToUserTopicAsync(string fcmToken, Guid userId, CancellationToken cancellationToken = default)
    {
        var userTopic = $"resq.user.{userId}";
        try
        {
            // Subscribe vào topic riêng của user
            var userResponse = await FirebaseMessaging.DefaultInstance.SubscribeToTopicAsync(new[] { fcmToken }, userTopic);
            _logger.LogInformation("FCM token subscribed to topic {Topic}. Success={Success}, Fail={Fail}", userTopic, userResponse.SuccessCount, userResponse.FailureCount);

            // Subscribe vào topic broadcast chung (dùng cho admin alert toàn hệ thống)
            var broadcastResponse = await FirebaseMessaging.DefaultInstance.SubscribeToTopicAsync(new[] { fcmToken }, "all_users");
            _logger.LogInformation("FCM token subscribed to topic all_users. Success={Success}, Fail={Fail}", broadcastResponse.SuccessCount, broadcastResponse.FailureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe FCM token to topic {Topic}", userTopic);
        }
    }

    public async Task UnsubscribeFromUserTopicAsync(string fcmToken, Guid userId, CancellationToken cancellationToken = default)
    {
        var userTopic = $"resq.user.{userId}";
        try
        {
            // Unsubscribe khỏi topic riêng của user
            var userResponse = await FirebaseMessaging.DefaultInstance.UnsubscribeFromTopicAsync(new[] { fcmToken }, userTopic);
            _logger.LogInformation("FCM token unsubscribed from topic {Topic}. Success={Success}, Fail={Fail}", userTopic, userResponse.SuccessCount, userResponse.FailureCount);

            // Unsubscribe khỏi topic broadcast chung
            var broadcastResponse = await FirebaseMessaging.DefaultInstance.UnsubscribeFromTopicAsync(new[] { fcmToken }, "all_users");
            _logger.LogInformation("FCM token unsubscribed from topic all_users. Success={Success}, Fail={Fail}", broadcastResponse.SuccessCount, broadcastResponse.FailureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe FCM token from topic {Topic}", userTopic);
        }
    }
}
