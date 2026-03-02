namespace RESQ.Domain.Entities.Emergency;

public class MissionAiSuggestionModel
{
    public int Id { get; set; }
    public int? ClusterId { get; set; }
    public string? ModelName { get; set; }
    public string? ModelVersion { get; set; }
    public string? AnalysisType { get; set; }
    public string? SuggestedMissionTitle { get; set; }
    public string? SuggestedMissionType { get; set; }
    public double? SuggestedPriorityScore { get; set; }
    public string? SuggestedSeverityLevel { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? SuggestionScope { get; set; }
    /// <summary>Full serialized AI response as JSON</summary>
    public string? Metadata { get; set; }
    public DateTime? CreatedAt { get; set; }

    public List<ActivityAiSuggestionModel> Activities { get; set; } = [];
}

public class ActivityAiSuggestionModel
{
    public int Id { get; set; }
    public int? ClusterId { get; set; }
    public int? ParentMissionSuggestionId { get; set; }
    public string? ModelName { get; set; }
    public string? ActivityType { get; set; }
    public string? SuggestionPhase { get; set; }
    /// <summary>Serialized List&lt;SuggestedActivityDto&gt; as JSON</summary>
    public string? SuggestedActivities { get; set; }
    public double? ConfidenceScore { get; set; }
    public DateTime? CreatedAt { get; set; }
}
