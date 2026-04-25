namespace RESQ.Application.Common;

public static class AiTextSanitizer
{
    private static readonly string[] BackendEnglishSuffixMarkers =
    [
        "AI suggested score",
        "AI agrees with the current rule-base score",
        "AI does not agree with the current rule-base score"
    ];

    public static string? RemoveBackendEnglishSuffix(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var cleaned = text.Trim();
        foreach (var marker in BackendEnglishSuffixMarkers)
        {
            var index = cleaned.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                cleaned = cleaned[..index].Trim();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(cleaned))
            return null;

        return EndsWithSentencePunctuation(cleaned)
            ? cleaned
            : $"{cleaned}.";
    }

    private static bool EndsWithSentencePunctuation(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var last = text.TrimEnd()[^1];
        return last is '.' or '!' or '?' or '。';
    }
}
