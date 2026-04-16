using System.Text.Json;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Emergency.Shared;

internal sealed class MissionAiSuggestionMetadataView
{
    public string? OverallAssessment { get; set; }
    public string? EstimatedDuration { get; set; }
    public string? SpecialNotes { get; set; }
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

        return DeserializeWithNamingFallback<MissionAiSuggestionMetadataView>(
            metadataJson,
            "needs_additional_depot",
            "supply_shortages",
            "overall_assessment");
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
