using System.Text.Json.Serialization;
using RESQ.Application.Common;
using RESQ.Application.UseCases.Emergency.Queries;
using RESQ.Application.UseCases.Emergency.Queries.GetSosEvaluation;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequests;

public class SosRequestDetailDto
{
    public int Id { get; set; }
    public Guid? PacketId { get; set; }
    public int? ClusterId { get; set; }
    public Guid UserId { get; set; }
    public string? SosType { get; set; }
    [JsonPropertyName("msg")]
    public string RawMessage { get; set; } = string.Empty;
    public SosStructuredDataDto? StructuredData { get; set; }
    public SosNetworkMetadataDto? NetworkMetadata { get; set; }
    public SosSenderInfoDto? SenderInfo { get; set; }
    public SosReporterInfoDto? ReporterInfo { get; set; }
    public SosVictimInfoDto? VictimInfo { get; set; }
    public bool IsSentOnBehalf { get; set; }
    public string? OriginId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PriorityLevel { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? LocationAccuracy { get; set; }
    public long? Timestamp { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedById { get; set; }
    public Guid? CreatedByCoordinatorId { get; set; }
    public SosRequestDetailEvaluationDto Evaluation { get; set; } = new();
    public string? LatestIncidentNote { get; set; }
    public DateTime? LatestIncidentAt { get; set; }
    public List<SosIncidentNoteDto>? IncidentHistory { get; set; }
    public List<CompanionResultDto>? Companions { get; set; }
}

public class SosRequestDetailEvaluationDto
{
    public SosRuleEvaluationDto? RuleEvaluation { get; set; }
    public SosRequestDetailAiAnalysisDto? AiAnalysis { get; set; }
    public bool HasAiAnalysis => AiAnalysis is not null;
}

public class SosRequestDetailAiAnalysisDto
{
    public int Id { get; set; }
    public string? SuggestedSeverityLevel { get; set; }
    public string? SuggestedPriority { get; set; }
    public double? SuggestedPriorityScore { get; set; }
    public bool? AgreesWithRuleBase { get; set; }
    public string? Explanation { get; set; }
    public bool? NeedsImmediateSafeTransfer { get; set; }
    public bool? CanWaitForCombinedMission { get; set; }
    public string? HandlingReason { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public static class SosRequestDetailAiAnalysisMapper
{
    public static SosRequestDetailAiAnalysisDto? Map(SosAiAnalysisModel? analysis)
    {
        if (analysis is null)
            return null;

        var summary = RESQ.Application.Services.SosRequestAiAnalysisHelper.FromAnalysis(analysis);
        var explanation = AiTextSanitizer.RemoveBackendEnglishSuffix(analysis.Explanation);

        return new SosRequestDetailAiAnalysisDto
        {
            Id = analysis.Id,
            SuggestedSeverityLevel = summary?.SuggestedSeverity ?? analysis.SuggestedSeverityLevel,
            SuggestedPriority = summary?.SuggestedPriority ?? analysis.SuggestedPriority,
            SuggestedPriorityScore = analysis.SuggestedPriorityScore,
            AgreesWithRuleBase = analysis.AgreesWithRuleBase,
            Explanation = explanation,
            NeedsImmediateSafeTransfer = summary?.NeedsImmediateSafeTransfer,
            CanWaitForCombinedMission = summary?.CanWaitForCombinedMission,
            HandlingReason = AiTextSanitizer.RemoveBackendEnglishSuffix(summary?.HandlingReason)
                ?? explanation,
            CreatedAt = analysis.CreatedAt
        };
    }
}
