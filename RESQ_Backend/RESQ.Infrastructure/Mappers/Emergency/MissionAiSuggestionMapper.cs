using System.Text.Json;
using RESQ.Domain.Entities.Emergency;
using RESQ.Infrastructure.Entities.Emergency;

namespace RESQ.Infrastructure.Mappers.Emergency;

public static class MissionAiSuggestionMapper
{
    public static MissionAiSuggestion ToEntity(MissionAiSuggestionModel model)
    {
        return new MissionAiSuggestion
        {
            ClusterId = model.ClusterId,
            ModelName = model.ModelName,
            ModelVersion = model.ModelVersion,
            AnalysisType = model.AnalysisType,
            SuggestedMissionTitle = model.SuggestedMissionTitle,
            SuggestedMissionType = model.SuggestedMissionType,
            SuggestedPriorityScore = model.SuggestedPriorityScore,
            SuggestedSeverityLevel = model.SuggestedSeverityLevel,
            SuggestionScope = model.SuggestionScope,
            Metadata = model.Metadata,
            CreatedAt = model.CreatedAt
        };
    }

    public static ActivityAiSuggestion ToActivityEntity(ActivityAiSuggestionModel model)
    {
        return new ActivityAiSuggestion
        {
            ClusterId = model.ClusterId,
            ParentMissionSuggestionId = model.ParentMissionSuggestionId,
            ModelName = model.ModelName,
            ActivityType = model.ActivityType,
            SuggestionPhase = model.SuggestionPhase,
            SuggestedActivities = model.SuggestedActivities,
            CreatedAt = model.CreatedAt
        };
    }

    public static MissionAiSuggestionModel ToDomain(MissionAiSuggestion entity, IEnumerable<ActivityAiSuggestion>? activities = null)
    {
        return new MissionAiSuggestionModel
        {
            Id = entity.Id,
            ClusterId = entity.ClusterId,
            ModelName = entity.ModelName,
            ModelVersion = entity.ModelVersion,
            AnalysisType = entity.AnalysisType,
            SuggestedMissionTitle = entity.SuggestedMissionTitle,
            SuggestedMissionType = entity.SuggestedMissionType,
            SuggestedPriorityScore = entity.SuggestedPriorityScore,
            SuggestedSeverityLevel = entity.SuggestedSeverityLevel,
            SuggestionScope = entity.SuggestionScope,
            Metadata = entity.Metadata,
            CreatedAt = entity.CreatedAt,
            Activities = activities?.Select(ToActivityDomain).ToList() ?? []
        };
    }

    public static ActivityAiSuggestionModel ToActivityDomain(ActivityAiSuggestion entity)
    {
        return new ActivityAiSuggestionModel
        {
            Id = entity.Id,
            ClusterId = entity.ClusterId,
            ParentMissionSuggestionId = entity.ParentMissionSuggestionId,
            ModelName = entity.ModelName,
            ActivityType = entity.ActivityType,
            SuggestionPhase = entity.SuggestionPhase,
            SuggestedActivities = entity.SuggestedActivities,
            CreatedAt = entity.CreatedAt
        };
    }
}
