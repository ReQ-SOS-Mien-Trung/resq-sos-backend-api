using RESQ.Application.Services;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.TestPrompt;

public class TestPromptResponse
{
    public bool IsSuccess { get; set; }
    public int? PromptId { get; set; }
    public string PromptName { get; set; } = string.Empty;
    public PromptType PromptType { get; set; }
    public int ClusterId { get; set; }
    public int? SuggestionId { get; set; }
    public int? AiConfigId { get; set; }
    public string? AiConfigVersion { get; set; }
    public AiProvider Provider { get; set; }
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int MaxTokens { get; set; }
    public string? ModelName { get; set; }
    public string? AiResponse { get; set; }
    public string? RawAiResponse { get; set; }
    public string? ErrorMessage { get; set; }
    public int? HttpStatusCode { get; set; }
    public long ResponseTimeMs { get; set; }
    public int SosRequestCount { get; set; }

    public string? SuggestedMissionTitle { get; set; }
    public string? SuggestedMissionType { get; set; }
    public double? SuggestedPriorityScore { get; set; }
    public string? SuggestedSeverityLevel { get; set; }
    public string? OverallAssessment { get; set; }

    public List<SuggestedActivityDto> SuggestedActivities { get; set; } = [];
    public List<SuggestedResourceDto> SuggestedResources { get; set; } = [];
    public string? EstimatedDuration { get; set; }
    public string? SpecialNotes { get; set; }
    public string MixedRescueReliefWarning { get; set; } = string.Empty;
    public bool NeedsAdditionalDepot { get; set; }
    public List<SupplyShortageDto> SupplyShortages { get; set; } = [];
    public double ConfidenceScore { get; set; }
    public bool NeedsManualReview { get; set; }
    public string? LowConfidenceWarning { get; set; }
    public bool MultiDepotRecommended { get; set; }
    public string? PipelineExecutionMode { get; set; }
    public string? PipelineStatus { get; set; }
    public string? PipelineFinalResultSource { get; set; }
    public string? PipelineFailedStage { get; set; }
    public string? PipelineFailureReason { get; set; }
    public MissionSuggestionPipelineMetadata? PipelineMetadata { get; set; }
}
