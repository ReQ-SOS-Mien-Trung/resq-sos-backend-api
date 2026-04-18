using System.Globalization;
using System.Text;

namespace RESQ.Application.Services;

public static class MissionSuggestionWarningHelper
{
    private const string LegacyMixedRescueReliefWarningMessage =
        "Kế hoạch đang gộp chung cứu hộ/cấp cứu với cứu trợ cấp phát. Nguyên tắc an toàn: sau khi cứu nạn nhân phải đưa họ về Safe Zone/Assembly Point ngay để cấp cứu, không tiếp tục cho nạn nhân đi phát đồ. Khuyến nghị tách thành mission riêng; coordinator chỉ nên bỏ qua cảnh báo này khi chủ động chấp nhận trách nhiệm.";

    public const string MixedRescueReliefWarningMessage = LegacyMixedRescueReliefWarningMessage;

    public static MissionSuggestionWarningNormalization NormalizeMixedRescueReliefWarning(
        string? specialNotes,
        string? mixedRescueReliefWarning,
        bool allowFallbackFromSpecialNotes)
    {
        var warning = NormalizeExplicitWarning(mixedRescueReliefWarning);
        var cleanedNotes = string.IsNullOrWhiteSpace(specialNotes)
            ? string.Empty
            : specialNotes.Trim();

        var shouldStripFromSpecialNotes = !string.IsNullOrWhiteSpace(warning) || allowFallbackFromSpecialNotes;
        if (!shouldStripFromSpecialNotes)
            return new MissionSuggestionWarningNormalization(cleanedNotes, string.Empty);

        var strippedNotes = StripWarningLines(cleanedNotes, out var warningFoundInNotes);
        if (warningFoundInNotes && string.IsNullOrWhiteSpace(warning))
            warning = MixedRescueReliefWarningMessage;

        return new MissionSuggestionWarningNormalization(strippedNotes, warning);
    }

    private static string NormalizeExplicitWarning(string? warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
            return string.Empty;

        return MixedRescueReliefWarningMessage;
    }

    private static string StripWarningLines(string notes, out bool warningFound)
    {
        warningFound = false;
        if (string.IsNullOrWhiteSpace(notes))
            return string.Empty;

        var normalizedNotes = notes.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalizedNotes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var keptLines = new List<string>();
        foreach (var line in lines)
        {
            if (LooksLikeMixedRescueReliefWarning(line))
            {
                warningFound = true;
                continue;
            }

            keptLines.Add(line);
        }

        if (warningFound)
            return string.Join(Environment.NewLine, keptLines);

        foreach (var warningVariant in GetKnownWarningVariants())
        {
            if (!normalizedNotes.Contains(warningVariant, StringComparison.Ordinal))
                continue;

            warningFound = true;
            return CleanupNotes(normalizedNotes.Replace(warningVariant, string.Empty, StringComparison.Ordinal));
        }

        if (LooksLikeMixedRescueReliefWarning(normalizedNotes))
        {
            warningFound = true;
            return string.Empty;
        }

        return normalizedNotes.Trim();
    }

    private static bool LooksLikeMixedRescueReliefWarning(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = NormalizeForComparison(text);
        return normalized.Contains("safe zone/assembly point", StringComparison.Ordinal)
            && ContainsNormalized(normalized, "tách thành mission")
            && ContainsNormalized(normalized, "cứu trợ cấp phát")
            && (ContainsNormalized(normalized, "cứu hộ/cấp cứu")
                || ContainsNormalized(normalized, "cứu hộ cấp cứu"));
    }

    private static bool ContainsNormalized(string normalizedSource, string phrase) =>
        normalizedSource.Contains(NormalizeForComparison(phrase), StringComparison.Ordinal);

    private static string NormalizeForComparison(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace("\u0111", "d", StringComparison.Ordinal);
    }

    private static IEnumerable<string> GetKnownWarningVariants()
    {
        yield return MixedRescueReliefWarningMessage;
        yield return LegacyMixedRescueReliefWarningMessage;
    }

    private static string CleanupNotes(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return string.Empty;

        return string.Join(
            Environment.NewLine,
            notes
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line)));
    }
}

public readonly record struct MissionSuggestionWarningNormalization(
    string SpecialNotes,
    string MixedRescueReliefWarning);
