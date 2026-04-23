using RESQ.Domain.Entities.Emergency;
using RESQ.Infrastructure.Entities.Emergency;

namespace RESQ.Infrastructure.Mappers.Emergency;

public static class SosAiAnalysisMapper
{
    public static SosAiAnalysis ToEntity(SosAiAnalysisModel model)
    {
        return new SosAiAnalysis
        {
            SosRequestId = model.SosRequestId,
            ModelName = model.ModelName,
            ModelVersion = model.ModelVersion,
            AnalysisType = model.AnalysisType,
            SuggestedSeverityLevel = model.SuggestedSeverityLevel,
            SuggestedPriority = model.SuggestedPriority,
            SuggestedPriorityScore = model.SuggestedPriorityScore,
            AgreesWithRuleBase = model.AgreesWithRuleBase,
            Explanation = model.Explanation,
            SuggestionScope = model.SuggestionScope,
            Metadata = model.Metadata,
            CreatedAt = model.CreatedAt,
            AdoptedAt = model.AdoptedAt
        };
    }

    public static SosAiAnalysisModel ToDomain(SosAiAnalysis entity)
    {
        return new SosAiAnalysisModel
        {
            Id = entity.Id,
            SosRequestId = entity.SosRequestId ?? 0,
            ModelName = entity.ModelName,
            ModelVersion = entity.ModelVersion,
            AnalysisType = entity.AnalysisType,
            SuggestedSeverityLevel = entity.SuggestedSeverityLevel,
            SuggestedPriority = entity.SuggestedPriority,
            SuggestedPriorityScore = entity.SuggestedPriorityScore,
            AgreesWithRuleBase = entity.AgreesWithRuleBase,
            Explanation = entity.Explanation,
            SuggestionScope = entity.SuggestionScope,
            Metadata = entity.Metadata,
            CreatedAt = entity.CreatedAt,
            AdoptedAt = entity.AdoptedAt
        };
    }
}
