using FirebaseAdmin.Auth;
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
}
