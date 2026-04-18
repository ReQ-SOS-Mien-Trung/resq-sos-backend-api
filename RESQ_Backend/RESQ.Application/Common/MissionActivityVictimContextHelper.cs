using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Emergency.Queries;

namespace RESQ.Application.Common;

public sealed class MissionActivityVictimContext
{
    public string? Summary { get; init; }
    public List<MissionActivityTargetVictimDto> Victims { get; init; } = [];
}

public static class MissionActivityVictimContextHelper
{
    private const string TargetVictimPrefix = "Đối tượng cần hỗ trợ:";

    private static readonly HashSet<string> ActivityTypesWithDescriptionSummary = new(StringComparer.OrdinalIgnoreCase)
    {
        "DELIVER_SUPPLIES",
        "RESCUE",
        "MEDICAL_AID",
        "EVACUATE"
    };

    public static MissionActivityVictimContext BuildContext(
        string? structuredDataJson,
        int? sosRequestId = null)
    {
        return BuildContext(SosStructuredDataParser.Parse(structuredDataJson), sosRequestId);
    }

    public static MissionActivityVictimContext BuildContext(
        SosStructuredDataDto? structuredData,
        int? sosRequestId = null)
    {
        if (structuredData is null)
            return new MissionActivityVictimContext();

        var victims = BuildVictims(structuredData);
        var summary = BuildSummary(victims, structuredData.Incident?.PeopleCount, sosRequestId);

        return new MissionActivityVictimContext
        {
            Summary = summary,
            Victims = victims
        };
    }

    public static List<MissionActivityTargetVictimDto> CloneVictims(
        IEnumerable<MissionActivityTargetVictimDto>? victims)
    {
        if (victims is null)
            return [];

        return victims.Select(victim => new MissionActivityTargetVictimDto
        {
            PersonId = victim.PersonId,
            DisplayName = victim.DisplayName,
            PersonType = victim.PersonType,
            PersonPhone = victim.PersonPhone,
            Index = victim.Index,
            IsInjured = victim.IsInjured,
            Severity = victim.Severity,
            MedicalIssues = victim.MedicalIssues?.ToList() ?? [],
            ClothingNeeded = victim.ClothingNeeded,
            ClothingGender = victim.ClothingGender,
            SpecialDietDescription = victim.SpecialDietDescription
        }).ToList();
    }

    public static string? ApplySummaryToDescription(
        string? activityType,
        string? description,
        string? targetVictimSummary)
    {
        var cleanedLines = (description ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !line.TrimStart().StartsWith(TargetVictimPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        while (cleanedLines.Count > 0 && string.IsNullOrWhiteSpace(cleanedLines[^1]))
            cleanedLines.RemoveAt(cleanedLines.Count - 1);

        if (!ShouldInjectSummaryIntoDescription(activityType) || string.IsNullOrWhiteSpace(targetVictimSummary))
            return cleanedLines.Count == 0 ? null : string.Join(Environment.NewLine, cleanedLines);

        cleanedLines.Add($"{TargetVictimPrefix} {EnsureSentence(targetVictimSummary)}");
        return string.Join(Environment.NewLine, cleanedLines);
    }

    public static bool ShouldInjectSummaryIntoDescription(string? activityType) =>
        !string.IsNullOrWhiteSpace(activityType)
        && ActivityTypesWithDescriptionSummary.Contains(activityType.Trim());

    private static List<MissionActivityTargetVictimDto> BuildVictims(SosStructuredDataDto structuredData)
    {
        var victims = (structuredData.Victims ?? [])
            .Select((victim, index) => MapVictim(victim, index + 1))
            .ToList();

        AppendAnonymousVictimsFromCounts(victims, structuredData.Incident?.PeopleCount);
        return victims;
    }

    private static MissionActivityTargetVictimDto MapVictim(SosVictimDto victim, int sequence)
    {
        var medicalIssues = victim.IncidentStatus?.MedicalIssues?
            .Where(issue => !string.IsNullOrWhiteSpace(issue))
            .Select(issue => issue.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        return new MissionActivityTargetVictimDto
        {
            PersonId = victim.PersonId,
            DisplayName = BuildDisplayName(
                victim.CustomName,
                victim.PersonType,
                victim.Index,
                sequence),
            PersonType = victim.PersonType,
            PersonPhone = victim.PersonPhone,
            Index = victim.Index,
            IsInjured = victim.IncidentStatus?.IsInjured,
            Severity = victim.IncidentStatus?.Severity,
            MedicalIssues = medicalIssues,
            ClothingNeeded = victim.PersonalNeeds?.Clothing?.Needed,
            ClothingGender = victim.PersonalNeeds?.Clothing?.Gender,
            SpecialDietDescription = victim.PersonalNeeds?.Diet?.Description
        };
    }

    private static void AppendAnonymousVictimsFromCounts(
        List<MissionActivityTargetVictimDto> victims,
        SosPeopleCountDto? peopleCount)
    {
        if (peopleCount is null)
            return;

        AppendAnonymousVictimsOfType(victims, "ADULT", peopleCount.Adult);
        AppendAnonymousVictimsOfType(victims, "CHILD", peopleCount.Child);
        AppendAnonymousVictimsOfType(victims, "ELDERLY", peopleCount.Elderly);
    }

    private static void AppendAnonymousVictimsOfType(
        List<MissionActivityTargetVictimDto> victims,
        string personType,
        int? totalCount)
    {
        var requiredCount = Math.Max(0, totalCount ?? 0);
        if (requiredCount == 0)
            return;

        var existingCount = victims.Count(victim =>
            string.Equals(victim.PersonType, personType, StringComparison.OrdinalIgnoreCase));

        for (var index = existingCount + 1; index <= requiredCount; index++)
        {
            victims.Add(new MissionActivityTargetVictimDto
            {
                DisplayName = BuildDisplayName(null, personType, index, victims.Count + 1),
                PersonType = personType,
                Index = index
            });
        }
    }

    private static string? BuildSummary(
        IReadOnlyList<MissionActivityTargetVictimDto> victims,
        SosPeopleCountDto? peopleCount,
        int? sosRequestId)
    {
        if (victims.Count > 0)
        {
            var labels = victims
                .Select(victim => BuildSummaryLabel(victim))
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToList();

            if (labels.Count == 0)
                return BuildCountSummary(peopleCount, sosRequestId);

            if (labels.Count <= 4)
                return string.Join(", ", labels);

            return $"{string.Join(", ", labels.Take(3))} và {labels.Count - 3} nạn nhân khác";
        }

        return BuildCountSummary(peopleCount, sosRequestId);
    }

    private static string BuildSummaryLabel(MissionActivityTargetVictimDto victim)
    {
        var displayName = string.IsNullOrWhiteSpace(victim.DisplayName)
            ? null
            : victim.DisplayName!.Trim();
        var typeLabel = GetPersonTypeLabel(victim.PersonType)?.ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(typeLabel))
            return $"{displayName} ({typeLabel})";

        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName!;

        return typeLabel ?? "nạn nhân";
    }

    private static string? BuildCountSummary(SosPeopleCountDto? peopleCount, int? sosRequestId)
    {
        if (peopleCount is null)
            return sosRequestId.HasValue ? $"Nạn nhân thuộc SOS #{sosRequestId.Value}" : null;

        var parts = new List<string>();
        if ((peopleCount.Adult ?? 0) > 0)
            parts.Add($"{peopleCount.Adult} người lớn");
        if ((peopleCount.Child ?? 0) > 0)
            parts.Add($"{peopleCount.Child} trẻ em");
        if ((peopleCount.Elderly ?? 0) > 0)
            parts.Add($"{peopleCount.Elderly} người già");

        if (parts.Count == 0)
            return sosRequestId.HasValue ? $"Nạn nhân thuộc SOS #{sosRequestId.Value}" : null;

        var total = (peopleCount.Adult ?? 0) + (peopleCount.Child ?? 0) + (peopleCount.Elderly ?? 0);
        if (total > 0)
            return $"{total} nạn nhân ({string.Join(", ", parts)})";

        return string.Join(", ", parts);
    }

    private static string BuildDisplayName(
        string? customName,
        string? personType,
        int? victimIndex,
        int fallbackSequence)
    {
        if (!string.IsNullOrWhiteSpace(customName))
            return customName.Trim();

        var typeLabel = GetPersonTypeLabel(personType) ?? "Nạn nhân";
        var index = victimIndex ?? fallbackSequence;
        return $"{typeLabel} #{Math.Max(index, 1)}";
    }

    private static string? GetPersonTypeLabel(string? personType) =>
        (personType ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "CHILD" => "Trẻ em",
            "ELDERLY" => "Người già",
            "ADULT" => "Người lớn",
            "PREGNANT" => "Thai phụ",
            _ => string.IsNullOrWhiteSpace(personType) ? null : personType
        };

    private static string EnsureSentence(string summary) =>
        summary.Trim().TrimEnd('.', '!', '?') + ".";
}
