namespace RESQ.Application.Common.Security;

public static class SecretMasker
{
    public static string? Mask(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return null;
        }

        if (secret.Length <= 8)
        {
            return new string('*', secret.Length);
        }

        return $"{secret[..4]}...{secret[^4..]}";
    }
}