using FirebaseAdmin.Auth;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Notifications;
using RESQ.Application.Services;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace RESQ.Infrastructure.Services;

public class FirebaseService(
    ILogger<FirebaseService> logger,
    INotificationRepository notificationRepository,
    INotificationHubService notificationHubService,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory) : IFirebaseService
{
    private readonly ILogger<FirebaseService> _logger = logger;
    private readonly INotificationRepository _notificationRepository = notificationRepository;
    private readonly INotificationHubService _notificationHubService = notificationHubService;
    private readonly IConfiguration _configuration = configuration;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

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

    public async Task<FirebaseGoogleUserInfo> VerifyGoogleIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
    {
        // Peek at the JWT payload to determine the issuer without full verification
        var issuer = PeekJwtIssuer(idToken);

        if (issuer != null && issuer.StartsWith("https://securetoken.google.com"))
        {
            return await VerifyFirebaseGoogleIdTokenAsync(idToken, cancellationToken);
        }
        else if (issuer == "https://accounts.google.com")
        {
            return await VerifyRawGoogleIdTokenAsync(idToken, cancellationToken);
        }

        _logger.LogWarning("Unknown token issuer: {issuer}", issuer);
        throw new UnauthorizedException("Token không hợp lệ");
    }

    private async Task<FirebaseGoogleUserInfo> VerifyFirebaseGoogleIdTokenAsync(string idToken, CancellationToken cancellationToken)
    {
        try
        {
            var decoded = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken, cancellationToken);

            var email = decoded.Claims.TryGetValue("email", out var emailObj) ? emailObj?.ToString() : null;
            if (string.IsNullOrEmpty(email))
                throw new UnauthorizedException("Firebase token không chứa email");

            var name = decoded.Claims.TryGetValue("name", out var nameObj) ? nameObj?.ToString() : null;
            var givenName = decoded.Claims.TryGetValue("given_name", out var givenNameObj) ? givenNameObj?.ToString() : null;
            var familyName = decoded.Claims.TryGetValue("family_name", out var familyNameObj) ? familyNameObj?.ToString() : null;

            _logger.LogInformation("Firebase Google token verified. UID={uid}, Email={email}", decoded.Uid, email);

            return new FirebaseGoogleUserInfo
            {
                Uid = decoded.Uid,
                Email = email,
                Name = name,
                GivenName = givenName,
                FamilyName = familyName,
            };
        }
        catch (FirebaseAuthException ex)
        {
            _logger.LogWarning("Firebase Google token verification failed: {msg}", ex.Message);
            throw new UnauthorizedException("Firebase token không hợp lệ hoặc đã hết hạn");
        }
    }

    private async Task<FirebaseGoogleUserInfo> VerifyRawGoogleIdTokenAsync(string idToken, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(
            $"https://oauth2.googleapis.com/tokeninfo?id_token={idToken}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google tokeninfo validation failed with status: {status}", response.StatusCode);
            throw new UnauthorizedException("Token Google không hợp lệ hoặc đã hết hạn");
        }

        var tokenInfo = await response.Content.ReadFromJsonAsync<GoogleRawTokenInfo>(cancellationToken: cancellationToken);
        if (tokenInfo is null)
            throw new UnauthorizedException("Token Google không hợp lệ");

        var expectedClientId = _configuration["GoogleAuth:ClientId"];
        if (!string.IsNullOrEmpty(expectedClientId) && tokenInfo.Aud != expectedClientId)
        {
            _logger.LogWarning("Google token aud mismatch. Expected: {expected}, Got: {actual}", expectedClientId, tokenInfo.Aud);
            throw new UnauthorizedException("Token Google không hợp lệ");
        }

        _logger.LogInformation("Raw Google ID token verified for email={email}", tokenInfo.Email);

        return new FirebaseGoogleUserInfo
        {
            Uid = tokenInfo.Sub ?? tokenInfo.Email,
            Email = tokenInfo.Email,
            Name = tokenInfo.Name,
            GivenName = tokenInfo.GivenName,
            FamilyName = tokenInfo.FamilyName,
        };
    }

    /// <summary>Decodes JWT payload without signature verification to peek at the issuer claim.</summary>
    private static string? PeekJwtIssuer(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return null;
            var payload = parts[1];
            // Base64Url decode
            var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/')));
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("iss", out var iss) ? iss.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private class GoogleRawTokenInfo
    {
        [JsonPropertyName("aud")] public string? Aud { get; set; }
        [JsonPropertyName("sub")] public string? Sub { get; set; }
        [JsonPropertyName("email")] public string Email { get; set; } = null!;
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("given_name")] public string? GivenName { get; set; }
        [JsonPropertyName("family_name")] public string? FamilyName { get; set; }
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
        await SendToTopicAsync(topic, title, body,
            new Dictionary<string, string> { ["type"] = type },
            cancellationToken);
    }

    public async Task SendToTopicAsync(string topic, string title, string body, Dictionary<string, string> data, CancellationToken cancellationToken = default)
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
                Data = data
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
            throw;
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
            throw;
        }
    }
}
