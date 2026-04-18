using System.Text.Json;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Emergency.Shared;

internal sealed class MissionAiSuggestionMetadataView
{
    public string? OverallAssessment { get; set; }
    public string? EstimatedDuration { get; set; }
    public string? SpecialNotes { get; set; }
    public string? MixedRescueReliefWarning { get; set; }
    public bool NeedsManualReview { get; set; }
    public string? LowConfidenceWarning { get; set; }
    public bool NeedsAdditionalDepot { get; set; }
    public List<SupplyShortageDto>? SupplyShortages { get; set; }
    public List<SuggestedResourceDto>? SuggestedResources { get; set; }
}

internal static class MissionAiSuggestionJsonHelper
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions SnakeCaseJsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    internal static MissionAiSuggestionMetadataView? ParseMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return null;

        var metadata = DeserializeWithNamingFallback<MissionAiSuggestionMetadataView>(
            metadataJson,
            "special_notes",
            "mixed_rescue_relief_warning",
            "needs_additional_depot",
            "supply_shortages",
            "overall_assessment");

        if (metadata is null)
            return null;

        var normalizedWarning = MissionSuggestionWarningHelper.NormalizeMixedRescueReliefWarning(
            metadata.SpecialNotes,
            metadata.MixedRescueReliefWarning,
            allowFallbackFromSpecialNotes: true);

        metadata.SpecialNotes = string.IsNullOrWhiteSpace(normalizedWarning.SpecialNotes)
            ? null
            : normalizedWarning.SpecialNotes;
        metadata.MixedRescueReliefWarning = normalizedWarning.MixedRescueReliefWarning;

        return metadata;
    }

    internal static List<SuggestedActivityDto> ParseActivities(string? activitiesJson)
    {
        if (string.IsNullOrWhiteSpace(activitiesJson))
            return [];

        return DeserializeWithNamingFallback<List<SuggestedActivityDto>>(
            activitiesJson,
            "activity_type",
            "estimated_time",
            "sos_request_id",
            "supplies_to_collect") ?? [];
    }

    internal static T? DeserializeWithNamingFallback<T>(string json, params string[] snakeCaseMarkers)
    {
        var prefersSnakeCase = snakeCaseMarkers.Any(marker =>
            json.Contains($"\"{marker}\"", StringComparison.Ordinal));

        var primaryOptions = prefersSnakeCase ? SnakeCaseJsonOpts : JsonOpts;
        var secondaryOptions = prefersSnakeCase ? JsonOpts : SnakeCaseJsonOpts;

        try
        {
            return JsonSerializer.Deserialize<T>(json, primaryOptions);
        }
        catch
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, secondaryOptions);
            }
            catch
            {
                return default;
            }
        }
    }
}
