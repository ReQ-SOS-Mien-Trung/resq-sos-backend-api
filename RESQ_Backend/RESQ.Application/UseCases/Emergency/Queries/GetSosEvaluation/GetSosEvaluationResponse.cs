using System.Text.Json;
using System.Text.Json.Serialization;
using RESQ.Application.Common;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosEvaluation;

public class SosRuleEvaluationDto
{
    public int Id { get; set; }
    public int? ConfigId { get; set; }
    public string? ConfigVersion { get; set; }
    public double MedicalScore { get; set; }
    public double InjuryScore { get; set; }
    public double MobilityScore { get; set; }
    public double EnvironmentScore { get; set; }
    public double FoodScore { get; set; }
    public double TotalScore { get; set; }
    public string PriorityLevel { get; set; } = string.Empty;
    public string RuleVersion { get; set; } = string.Empty;
    public List<string> ItemsNeeded { get; set; } = [];
    public SosPriorityEvaluationDetails? Breakdown { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SosAiAnalysisDto
{
    public int Id { get; set; }
    public string? ModelName { get; set; }
    public string? ModelVersion { get; set; }
    public string? AnalysisType { get; set; }
    public string? SuggestedSeverityLevel { get; set; }
    public string? SuggestedPriority { get; set; }
    public double? SuggestedPriorityScore { get; set; }
    public bool? AgreesWithRuleBase { get; set; }
    public string? Explanation { get; set; }
    public bool? NeedsImmediateSafeTransfer { get; set; }
    public bool? CanWaitForCombinedMission { get; set; }
    public string? HandlingReason { get; set; }
    public string? SuggestionScope { get; set; }
    public JsonElement? Metadata { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? AdoptedAt { get; set; }
}

public class SosRequestEvaluationDto
{
    public SosRuleEvaluationDto? RuleEvaluation { get; set; }
    public List<SosAiAnalysisDto> AiAnalyses { get; set; } = [];
    public bool HasAiAnalysis => AiAnalyses.Count > 0;
}

public class GetSosEvaluationResponse
{
    public int SosRequestId { get; set; }
    public string? SosType { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CurrentPriorityLevel { get; set; }
    public SosRequestEvaluationDto Evaluation { get; set; } = new();

    [JsonIgnore]
    public SosRuleEvaluationDto? RuleEvaluation => Evaluation.RuleEvaluation;

    [JsonIgnore]
    public List<SosAiAnalysisDto> AiAnalyses => Evaluation.AiAnalyses;

    [JsonIgnore]
    public bool HasAiAnalysis => Evaluation.HasAiAnalysis;
}

public static class SosEvaluationViewFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static SosRequestEvaluationDto CreateEvaluation(
        SosRuleEvaluationModel? ruleEvaluation,
        IEnumerable<SosAiAnalysisModel> aiAnalyses)
    {
        return new SosRequestEvaluationDto
        {
            RuleEvaluation = CreateRuleEvaluation(ruleEvaluation),
            AiAnalyses = aiAnalyses.Select(MapAiAnalysis).ToList()
        };
    }

    public static SosRuleEvaluationDto? CreateRuleEvaluation(SosRuleEvaluationModel? ruleEvaluation)
    {
        if (ruleEvaluation is null)
            return null;

        return new SosRuleEvaluationDto
        {
            Id = ruleEvaluation.Id,
            ConfigId = ruleEvaluation.ConfigId,
            ConfigVersion = ruleEvaluation.ConfigVersion,
            MedicalScore = ruleEvaluation.MedicalScore,
            InjuryScore = ruleEvaluation.InjuryScore,
            MobilityScore = ruleEvaluation.MobilityScore,
            EnvironmentScore = ruleEvaluation.EnvironmentScore,
            FoodScore = ruleEvaluation.FoodScore,
            TotalScore = ruleEvaluation.TotalScore,
            PriorityLevel = ruleEvaluation.PriorityLevel.ToString(),
            RuleVersion = ruleEvaluation.RuleVersion,
            ItemsNeeded = DeserializeItems(ruleEvaluation.ItemsNeeded),
            Breakdown = ParseJson<SosPriorityEvaluationDetails>(ruleEvaluation.BreakdownJson ?? ruleEvaluation.DetailsJson),
            CreatedAt = ruleEvaluation.CreatedAt
        };
    }

    private static SosAiAnalysisDto MapAiAnalysis(SosAiAnalysisModel analysis)
    {
        var metadata = ParseJson<JsonElement>(analysis.Metadata);
        var metadataModel = ParseJson<SosAiAnalysisMetadata>(analysis.Metadata);

        return new SosAiAnalysisDto
        {
            Id = analysis.Id,
            ModelName = analysis.ModelName,
            ModelVersion = analysis.ModelVersion,
            AnalysisType = analysis.AnalysisType,
            SuggestedSeverityLevel = analysis.SuggestedSeverityLevel
                ?? metadataModel?.AnalysisResult?.SuggestedSeverityLevel
                ?? metadataModel?.AnalysisResult?.SeverityLevel,
            SuggestedPriority = analysis.SuggestedPriority
                ?? metadataModel?.AnalysisResult?.SuggestedPriority
                ?? metadataModel?.AnalysisResult?.Priority,
            SuggestedPriorityScore = analysis.SuggestedPriorityScore
                ?? metadataModel?.AnalysisResult?.SuggestedPriorityScore,
            AgreesWithRuleBase = analysis.AgreesWithRuleBase
                ?? metadataModel?.AnalysisResult?.AgreesWithRuleBase,
            Explanation = AiTextSanitizer.RemoveBackendEnglishSuffix(
                analysis.Explanation
                ?? metadataModel?.AnalysisResult?.Explanation),
            NeedsImmediateSafeTransfer = metadataModel?.AnalysisResult?.NeedsImmediateSafeTransfer,
            CanWaitForCombinedMission = metadataModel?.AnalysisResult?.CanWaitForCombinedMission,
            HandlingReason = AiTextSanitizer.RemoveBackendEnglishSuffix(
                metadataModel?.AnalysisResult?.HandlingReason
                ?? analysis.Explanation),
            SuggestionScope = analysis.SuggestionScope,
            Metadata = metadata,
            CreatedAt = analysis.CreatedAt,
            AdoptedAt = analysis.AdoptedAt
        };
    }

    private static List<string> DeserializeItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static T? ParseJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return default;
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

        [JsonPropertyName("suggested_severity_level")]
        public string? SuggestedSeverityLevel { get; set; }

        [JsonPropertyName("suggested_priority_score")]
        public double? SuggestedPriorityScore { get; set; }

        [JsonPropertyName("agrees_with_rule_base")]
        public bool? AgreesWithRuleBase { get; set; }

        [JsonPropertyName("explanation")]
        public string? Explanation { get; set; }

        [JsonPropertyName("needs_immediate_safe_transfer")]
        public bool? NeedsImmediateSafeTransfer { get; set; }

        [JsonPropertyName("can_wait_for_combined_mission")]
        public bool? CanWaitForCombinedMission { get; set; }

        [JsonPropertyName("handling_reason")]
        public string? HandlingReason { get; set; }
    }
}
