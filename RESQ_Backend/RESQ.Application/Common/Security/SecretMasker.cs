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

    public static bool IsMasked(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Length > 0 && value.All(c => c == '*'))
        {
            return true;
        }

        if (value.Length == 11 && value.Substring(4, 3) == "...")
        {
            return true;
        }

        return false;
    }
}
