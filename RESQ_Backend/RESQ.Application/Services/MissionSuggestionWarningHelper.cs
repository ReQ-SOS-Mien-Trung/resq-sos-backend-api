using System.Globalization;
using System.Text;

namespace RESQ.Application.Services;

public static class MissionSuggestionWarningHelper
{
    private const string MixedRescueReliefWarningStart =
        "Kế hoạch đang gộp chung cứu hộ/cấp cứu với cứu trợ cấp phát.";

    private static readonly string LegacyMixedRescueReliefWarningStart =
        CreateLegacyWarningStart(MixedRescueReliefWarningStart);

    public const string MixedRescueReliefWarningMessage =
        "Kế hoạch đang gộp chung cứu hộ/cấp cứu với cứu trợ cấp phát. " +
        "Nguyên tắc an toàn: sau khi cứu nạn nhân phải đưa họ về điểm an toàn hoặc điểm tập kết ngay để cấp cứu, " +
        "không tiếp tục cho nạn nhân đi theo luồng cấp phát vật phẩm. " +
        "Khuyến nghị tách cứu hộ/cấp cứu và cứu trợ/cấp phát thành 2 nhiệm vụ riêng. " +
        "Điều phối viên chỉ nên bỏ qua cảnh báo này khi chủ động chấp nhận trách nhiệm.";

    public static string ResolveMixedRescueReliefWarning(
        IEnumerable<SuggestedActivityDto>? activities,
        string? explicitWarning)
    {
        var recomputedWarning = BuildMixedRescueReliefWarning(activities);
        return !string.IsNullOrWhiteSpace(recomputedWarning)
            ? recomputedWarning
            : NormalizeExplicitWarning(explicitWarning);
    }

    public static string BuildMixedRescueReliefWarning(IEnumerable<SuggestedActivityDto>? activities)
    {
        var activityList = activities?.ToList() ?? [];
        if (activityList.Count == 0)
            return string.Empty;

        var rescueActivities = activityList
            .Where(IsRescueOrMedicalActivity)
            .ToList();
        var reliefActivities = activityList
            .Where(IsReliefActivity)
            .ToList();

        if (rescueActivities.Count == 0 || reliefActivities.Count == 0)
            return string.Empty;

        var rescueSosIds = rescueActivities
            .Where(activity => activity.SosRequestId.HasValue)
            .Select(activity => activity.SosRequestId!.Value)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        var reliefSosIds = reliefActivities
            .Where(activity => activity.SosRequestId.HasValue)
            .Select(activity => activity.SosRequestId!.Value)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        if (rescueSosIds.Count == 0 || reliefSosIds.Count == 0)
            return MixedRescueReliefWarningMessage;

        return
            "Kế hoạch đang gộp chung cứu hộ/cấp cứu với cứu trợ cấp phát. " +
            "Nguyên tắc an toàn: sau khi cứu nạn nhân phải đưa họ về điểm an toàn hoặc điểm tập kết ngay để cấp cứu, " +
            "không tiếp tục cho nạn nhân đi theo luồng cấp phát vật phẩm. " +
            $"Khuyến nghị tách thành 2 nhiệm vụ riêng: nhiệm vụ cứu hộ/cấp cứu cho {FormatSosGroup(rescueSosIds)}; " +
            $"nhiệm vụ cứu trợ/cấp phát cho {FormatSosGroup(reliefSosIds)}. " +
            "Điều phối viên chỉ nên bỏ qua cảnh báo này khi chủ động chấp nhận trách nhiệm.";
    }

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

        var trimmedWarning = warning.Trim();
        if (!LooksLikeMixedRescueReliefWarning(trimmedWarning))
            return trimmedWarning;

        return ContainsSplitSosGroups(trimmedWarning) && IsNormalizedVietnameseWarning(trimmedWarning)
            ? trimmedWarning
            : MixedRescueReliefWarningMessage;
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
            return CleanupNotes(string.Join(Environment.NewLine, keptLines));

        var removedTail = RemoveWarningTail(normalizedNotes, out warningFound);
        if (warningFound)
            return CleanupNotes(removedTail);

        if (LooksLikeMixedRescueReliefWarning(normalizedNotes))
        {
            warningFound = true;
            return string.Empty;
        }

        return normalizedNotes.Trim();
    }

    private static string RemoveWarningTail(string notes, out bool warningFound)
    {
        warningFound = false;
        foreach (var warningStart in GetKnownWarningStarts())
        {
            var index = notes.IndexOf(warningStart, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                continue;

            warningFound = true;
            return notes[..index];
        }

        return notes;
    }

    private static bool ContainsSplitSosGroups(string warning)
    {
        var normalized = NormalizeForComparison(warning);
        return ContainsNormalized(normalized, "nhiệm vụ cứu hộ/cấp cứu cho SOS #")
            && ContainsNormalized(normalized, "nhiệm vụ cứu trợ/cấp phát cho SOS #");
    }

    private static bool IsNormalizedVietnameseWarning(string warning)
    {
        var normalized = NormalizeForComparison(warning);
        return ContainsNormalized(normalized, "điểm an toàn")
            && ContainsNormalized(normalized, "điểm tập kết")
            && ContainsNormalized(normalized, "điều phối viên")
            && !normalized.Contains("safe zone", StringComparison.Ordinal)
            && !normalized.Contains("assembly point", StringComparison.Ordinal)
            && !ContainsNormalized(normalized, "tách thành mission riêng")
            && !normalized.Contains("coordinator", StringComparison.Ordinal);
    }

    private static bool LooksLikeMixedRescueReliefWarning(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = NormalizeForComparison(text);
        var hasMixedBranches =
            ContainsNormalized(normalized, "cứu trợ cấp phát")
            && (ContainsNormalized(normalized, "cứu hộ/cấp cứu")
                || ContainsNormalized(normalized, "cứu hộ cấp cứu"));

        var hasSafetyInstruction =
            normalized.Contains("safe zone/assembly point", StringComparison.Ordinal)
            || (ContainsNormalized(normalized, "điểm an toàn")
                && ContainsNormalized(normalized, "điểm tập kết"));

        var hasSplitRecommendation =
            ContainsNormalized(normalized, "tách thành mission riêng")
            || ContainsNormalized(normalized, "tách thành 2 nhiệm vụ riêng")
            || ContainsNormalized(normalized, "tách cứu hộ/cấp cứu và cứu trợ/cấp phát thành 2 nhiệm vụ riêng");

        return hasMixedBranches && hasSafetyInstruction && hasSplitRecommendation;
    }

    private static bool IsRescueOrMedicalActivity(SuggestedActivityDto activity) =>
        string.Equals(activity.ActivityType, "RESCUE", StringComparison.OrdinalIgnoreCase)
        || string.Equals(activity.ActivityType, "EVACUATE", StringComparison.OrdinalIgnoreCase)
        || string.Equals(activity.ActivityType, "MEDICAL_AID", StringComparison.OrdinalIgnoreCase);

    private static bool IsReliefActivity(SuggestedActivityDto activity) =>
        string.Equals(activity.ActivityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase)
        || string.Equals(activity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase);

    private static string FormatSosGroup(IReadOnlyCollection<int> sosIds)
    {
        var joinedIds = string.Join(", ", sosIds.Select(id => $"#{id}"));
        return sosIds.Count == 1
            ? $"SOS {joinedIds}"
            : $"các SOS {joinedIds}";
    }

    private static IEnumerable<string> GetKnownWarningStarts()
    {
        yield return MixedRescueReliefWarningStart;
        yield return LegacyMixedRescueReliefWarningStart;
    }

    private static bool ContainsNormalized(string normalizedText, string phrase) =>
        normalizedText.Contains(NormalizeForComparison(phrase), StringComparison.Ordinal);

    private static string CreateLegacyWarningStart(string warningStart)
    {
        var normalized = warningStart.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                _ => character
            });
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

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
            .Replace("đ", "d", StringComparison.Ordinal);
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
