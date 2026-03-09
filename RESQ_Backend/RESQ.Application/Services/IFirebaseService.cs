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
}
