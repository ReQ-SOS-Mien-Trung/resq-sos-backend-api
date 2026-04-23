using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Emergency.Commands.GenerateRescueMissionSuggestion;

public class GenerateRescueMissionSuggestionResponse
{
    /// <summary>ID bản ghi mission suggestion đã lưu vào DB</summary>
    public int? SuggestionId { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ModelName { get; set; }
    public double ResponseTimeMs { get; set; }
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

    /// <summary>true khi AI không đủ tự tin - người điều phối nên xem xét và điều chỉnh thủ công.</summary>
    public bool NeedsManualReview { get; set; }
    /// <summary>Lý do cần xem xét thủ công, ví dụ: "Độ tự tin AI chỉ đạt 45%, dưới ngưỡng 65%."</summary>
    /// <summary>true khi AI được gợi ý phối hợp nhiều kho vì không kho nào đủ đồ cho một lần cấp phát.</summary>
    public bool MultiDepotRecommended { get; set; }
    public string? PipelineExecutionMode { get; set; }
    public string? PipelineStatus { get; set; }
    public string? PipelineFinalResultSource { get; set; }
    public string? PipelineFailedStage { get; set; }
    public string? PipelineFailureReason { get; set; }
    public bool? PipelineUsedLegacyFallback { get; set; }
}
