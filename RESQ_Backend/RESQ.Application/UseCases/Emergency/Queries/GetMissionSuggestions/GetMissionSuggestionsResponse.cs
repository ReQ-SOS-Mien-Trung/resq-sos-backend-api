using System.Text.Json;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Emergency.Queries.GetMissionSuggestions;

public class GetMissionSuggestionsResponse
{
    public int ClusterId { get; set; }
    public int TotalSuggestions { get; set; }
    public List<MissionSuggestionDto> MissionSuggestions { get; set; } = [];
}

public class MissionSuggestionDto
{
    public int Id { get; set; }
    public int? ClusterId { get; set; }
    public string? ModelName { get; set; }
    public string? AnalysisType { get; set; }
    public string? SuggestedMissionTitle { get; set; }
    public string? SuggestedMissionType { get; set; }
    public double? SuggestedPriorityScore { get; set; }
    public string? SuggestedSeverityLevel { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? OverallAssessment { get; set; }
    public string? EstimatedDuration { get; set; }
    public string? SpecialNotes { get; set; }
    public string MixedRescueReliefWarning { get; set; } = string.Empty;
    public bool NeedsManualReview { get; set; }
    public string? LowConfidenceWarning { get; set; }
    public bool NeedsAdditionalDepot { get; set; }
    public List<SupplyShortageDto> SupplyShortages { get; set; } = [];
    public List<SuggestedResourceDto> SuggestedResources { get; set; } = [];
    public string? SuggestionScope { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<ActivitySuggestionDto> Activities { get; set; } = [];
}

public class ActivitySuggestionDto
{
    public int Id { get; set; }
    public string? ActivityType { get; set; }
    public string? SuggestionPhase { get; set; }
    public List<SuggestedActivityDto> SuggestedActivities { get; set; } = [];
    public double? ConfidenceScore { get; set; }
    public DateTime? CreatedAt { get; set; }
}
