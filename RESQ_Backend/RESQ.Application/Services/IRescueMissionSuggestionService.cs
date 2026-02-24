namespace RESQ.Application.Services;

public interface IRescueMissionSuggestionService
{
    Task<RescueMissionSuggestionResult> GenerateSuggestionAsync(
        List<SosRequestSummary> sosRequests,
        CancellationToken cancellationToken = default);
}

public class SosRequestSummary
{
    public int Id { get; set; }
    public string? SosType { get; set; }
    public string RawMessage { get; set; } = string.Empty;
    public string? StructuredData { get; set; }
    public string? PriorityLevel { get; set; }
    public string? Status { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? WaitTimeMinutes { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class RescueMissionSuggestionResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ModelName { get; set; }
    public double ResponseTimeMs { get; set; }

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
    public string? RawAiResponse { get; set; }
}

public class SuggestedActivityDto
{
    public int Step { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Priority { get; set; }
    public string? EstimatedTime { get; set; }
}

public class SuggestedResourceDto
{
    public string ResourceType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? Quantity { get; set; }
    public string? Priority { get; set; }
}
