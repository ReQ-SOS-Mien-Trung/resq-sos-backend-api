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
    public double ConfidenceScore { get; set; }
}
