using System.Text.Json;
using System.Text.Json.Serialization;
using RESQ.Application.Common;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Services;

public class SosRequestAiAnalysisSummary
{
    public bool HasAiAnalysis { get; set; }
    public string? SuggestedPriority { get; set; }
    public string? SuggestedSeverity { get; set; }
    public bool? NeedsImmediateSafeTransfer { get; set; }
    public bool? CanWaitForCombinedMission { get; set; }
    public string? HandlingReason { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public static class SosRequestAiAnalysisHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static SosRequestAiAnalysisSummary CreateFallback(
        string? suggestedPriority,
        string? suggestedSeverity = null,
        string? handlingReason = null)
    {
        var normalizedPriority = NormalizePriority(suggestedPriority);
        return new SosRequestAiAnalysisSummary
        {
            HasAiAnalysis = false,
            SuggestedPriority = normalizedPriority,
            SuggestedSeverity = string.IsNullOrWhiteSpace(suggestedSeverity)
                ? MapSeverityFromPriority(normalizedPriority)
                : suggestedSeverity,
            NeedsImmediateSafeTransfer = string.Equals(normalizedPriority, "Critical", StringComparison.OrdinalIgnoreCase)
                ? true
                : null,
            CanWaitForCombinedMission = string.Equals(normalizedPriority, "Critical", StringComparison.OrdinalIgnoreCase)
                ? false
                : null,
            HandlingReason = handlingReason
        };
    }

    public static SosRequestAiAnalysisSummary? FromAnalysis(SosAiAnalysisModel? analysis)
    {
        if (analysis is null)
            return null;

        var metadata = ParseMetadata(analysis.Metadata);
        var suggestedPriority = NormalizePriority(
            metadata.AnalysisResult?.SuggestedPriority
            ?? metadata.AnalysisResult?.Priority
            ?? analysis.SuggestedPriority);

        var suggestedSeverity = metadata.AnalysisResult?.SuggestedSeverity
            ?? metadata.AnalysisResult?.SeverityLevel
            ?? analysis.SuggestedSeverityLevel;
        var handlingReason = AiTextSanitizer.RemoveBackendEnglishSuffix(
            metadata.AnalysisResult?.HandlingReason
            ?? analysis.Explanation);

        return new SosRequestAiAnalysisSummary
        {
            HasAiAnalysis = true,
            SuggestedPriority = suggestedPriority,
            SuggestedSeverity = string.IsNullOrWhiteSpace(suggestedSeverity)
                ? MapSeverityFromPriority(suggestedPriority)
                : suggestedSeverity,
            NeedsImmediateSafeTransfer = metadata.AnalysisResult?.NeedsImmediateSafeTransfer,
            CanWaitForCombinedMission = metadata.AnalysisResult?.CanWaitForCombinedMission,
            HandlingReason = handlingReason,
            CreatedAt = analysis.CreatedAt
        };
    }

    public static string? ResolveSuggestedPriority(
        SosRequestAiAnalysisSummary? summary,
        string? fallbackPriority = null)
    {
        return NormalizePriority(summary?.SuggestedPriority)
            ?? NormalizePriority(fallbackPriority);
    }

    public static bool HasUrgentMixedMissionConstraint(
        SosRequestAiAnalysisSummary? summary,
        string? fallbackPriority = null)
    {
        if (summary?.NeedsImmediateSafeTransfer == true)
            return true;

        if (summary?.CanWaitForCombinedMission == false)
            return true;

        var priority = ResolveSuggestedPriority(summary, fallbackPriority);
        return string.Equals(priority, "Critical", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRescueLikeRequestType(string? sosType)
    {
        return NormalizeRequestType(sosType) is "RESCUE" or "BOTH";
    }

    public static bool IsReliefRequestType(string? sosType)
    {
        return NormalizeRequestType(sosType) is "RELIEF" or "BOTH";
    }

    private static string NormalizeRequestType(string? sosType)
    {
        return SosPriorityRuleConfigSupport.NormalizeKey(sosType) switch
        {
            "SUPPLY" or "RELIEF" => "RELIEF",
            "RESCUE" or "MEDICAL" or "EVACUATION" => "RESCUE",
            "BOTH" or "MIXED" => "BOTH",
            _ => "OTHER"
        };
    }

    private static string? NormalizePriority(string? priority)
    {
        return SosPriorityRuleConfigSupport.NormalizeKey(priority) switch
        {
            "CRITICAL" => "Critical",
            "HIGH" => "High",
            "MEDIUM" or "MODERATE" => "Medium",
            "LOW" or "MINOR" => "Low",
            _ => string.IsNullOrWhiteSpace(priority) ? null : priority.Trim()
        };
    }

    private static string? MapSeverityFromPriority(string? priority)
    {
        return NormalizePriority(priority) switch
        {
            "Critical" => "Critical",
            "High" => "Severe",
            "Medium" => "Moderate",
            "Low" => "Minor",
            _ => null
        };
    }

    private static SosAiAnalysisMetadata ParseMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return new SosAiAnalysisMetadata();

        try
        {
            return JsonSerializer.Deserialize<SosAiAnalysisMetadata>(metadataJson, JsonOptions)
                ?? new SosAiAnalysisMetadata();
        }
        catch
        {
            return new SosAiAnalysisMetadata();
        }
    }

    private sealed class SosAiAnalysisMetadata
    {
        [JsonPropertyName("analysisResult")]
        public SosAiAnalysisMetadataResult? AnalysisResult { get; set; }
    }

    private sealed class SosAiAnalysisMetadataResult
    {
        [JsonPropertyName("priority")]
        public string? Priority { get; set; }

        [JsonPropertyName("suggested_priority")]
        public string? SuggestedPriority { get; set; }

        [JsonPropertyName("severity_level")]
        public string? SeverityLevel { get; set; }

        [JsonPropertyName("suggested_severity")]
        public string? SuggestedSeverity { get; set; }

        [JsonPropertyName("needs_immediate_safe_transfer")]
        public bool? NeedsImmediateSafeTransfer { get; set; }

        [JsonPropertyName("can_wait_for_combined_mission")]
        public bool? CanWaitForCombinedMission { get; set; }

        [JsonPropertyName("handling_reason")]
        public string? HandlingReason { get; set; }
    }
}
