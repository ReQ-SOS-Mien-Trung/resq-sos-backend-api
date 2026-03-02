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
    public double? SuggestedPriorityScore { get; set; }
    public double? ConfidenceScore { get; set; }
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
