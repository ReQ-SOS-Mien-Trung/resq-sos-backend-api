namespace RESQ.Domain.Entities.Emergency;

public class SosAiAnalysisModel
{
    public int Id { get; set; }
    public int SosRequestId { get; set; }
    public string? ModelName { get; set; }
    public string? ModelVersion { get; set; }
    public string? AnalysisType { get; set; }
    public string? SuggestedSeverityLevel { get; set; }
    public string? SuggestedPriority { get; set; }
    public string? Explanation { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? SuggestionScope { get; set; }
    public string? Metadata { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? AdoptedAt { get; set; }

    public SosAiAnalysisModel() { }

    public static SosAiAnalysisModel Create(
        int sosRequestId,
        string modelName,
        string? modelVersion,
        string analysisType,
        string suggestedSeverityLevel,
        string suggestedPriority,
        string explanation,
        double confidenceScore,
        string? suggestionScope = null,
        string? metadata = null)
    {
        return new SosAiAnalysisModel
        {
            SosRequestId = sosRequestId,
            ModelName = modelName,
            ModelVersion = modelVersion,
            AnalysisType = analysisType,
            SuggestedSeverityLevel = suggestedSeverityLevel,
            SuggestedPriority = suggestedPriority,
            Explanation = explanation,
            ConfidenceScore = confidenceScore,
            SuggestionScope = suggestionScope,
            Metadata = metadata,
            CreatedAt = DateTime.UtcNow
        };
    }
}
