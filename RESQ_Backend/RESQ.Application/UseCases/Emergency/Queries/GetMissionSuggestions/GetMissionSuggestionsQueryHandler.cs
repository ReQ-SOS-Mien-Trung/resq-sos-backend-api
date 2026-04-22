using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Emergency.Shared;

namespace RESQ.Application.UseCases.Emergency.Queries.GetMissionSuggestions;

public class GetMissionSuggestionsQueryHandler(
    IMissionAiSuggestionRepository missionRepository,
    ISosClusterRepository sosClusterRepository,
    ILogger<GetMissionSuggestionsQueryHandler> logger
) : IRequestHandler<GetMissionSuggestionsQuery, GetMissionSuggestionsResponse>
{
    private static readonly IReadOnlyDictionary<string, int> SuggestionPhaseRank =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Validated"] = 0,
            ["Execution"] = 1,
            ["Draft"] = 2
        };

    private readonly IMissionAiSuggestionRepository _missionRepository = missionRepository;
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ILogger<GetMissionSuggestionsQueryHandler> _logger = logger;

    public async Task<GetMissionSuggestionsResponse> Handle(
        GetMissionSuggestionsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting mission suggestions for ClusterId={clusterId}", request.ClusterId);

        var cluster = await _sosClusterRepository.GetByIdAsync(request.ClusterId, cancellationToken);
        if (cluster is null)
            throw new NotFoundException($"Không tìm thấy cluster với ID: {request.ClusterId}");

        var suggestions = (await _missionRepository.GetByClusterIdAsync(request.ClusterId, cancellationToken))
            .OrderByDescending(suggestion => suggestion.CreatedAt ?? DateTime.MinValue)
            .ThenByDescending(suggestion => suggestion.Id)
            .ToList();

        var missionDtos = suggestions.Select(m =>
        {
            var metadata = MissionAiSuggestionJsonHelper.ParseMetadata(m.Metadata);
            var activityGroups = m.Activities.Select(a => new ActivitySuggestionDto
            {
                Id = a.Id,
                ActivityType = a.ActivityType,
                SuggestionPhase = a.SuggestionPhase,
                ConfidenceScore = a.ConfidenceScore,
                CreatedAt = a.CreatedAt,
                SuggestedActivities = MissionAiSuggestionJsonHelper.ParseActivities(a.SuggestedActivities)
            })
            .OrderByDescending(activityGroup => activityGroup.SuggestedActivities.Count > 0)
            .ThenBy(activityGroup => GetSuggestionPhaseRank(activityGroup.SuggestionPhase))
            .ThenByDescending(activityGroup => activityGroup.CreatedAt ?? DateTime.MinValue)
            .ThenByDescending(activityGroup => activityGroup.Id)
            .ToList();
            var mixedRescueReliefWarning = MissionSuggestionWarningHelper.ResolveMixedRescueReliefWarning(
                activityGroups.SelectMany(activityGroup => activityGroup.SuggestedActivities),
                metadata?.MixedRescueReliefWarning);

            return new MissionSuggestionDto
            {
                Id = m.Id,
                ClusterId = m.ClusterId,
                ModelName = m.ModelName,
                AnalysisType = m.AnalysisType,
                SuggestedMissionTitle = m.SuggestedMissionTitle,
                SuggestedMissionType = m.SuggestedMissionType ?? metadata?.SuggestedMissionType,
                SuggestedPriorityScore = m.SuggestedPriorityScore,
                SuggestedSeverityLevel = m.SuggestedSeverityLevel ?? metadata?.SuggestedSeverityLevel,
                ConfidenceScore = m.ConfidenceScore,
                OverallAssessment = metadata?.OverallAssessment,
                EstimatedDuration = metadata?.EstimatedDuration,
                SpecialNotes = metadata?.SpecialNotes,
                MixedRescueReliefWarning = mixedRescueReliefWarning,
                NeedsManualReview = (metadata?.NeedsManualReview ?? false) || !string.IsNullOrWhiteSpace(mixedRescueReliefWarning),
                LowConfidenceWarning = metadata?.LowConfidenceWarning,
                NeedsAdditionalDepot = metadata?.NeedsAdditionalDepot ?? false,
                SupplyShortages = metadata?.SupplyShortages ?? [],
                SuggestedResources = metadata?.SuggestedResources ?? [],
                SuggestionScope = m.SuggestionScope,
                CreatedAt = m.CreatedAt,
                Activities = activityGroups
            };
        }).ToList();

        return new GetMissionSuggestionsResponse
        {
            ClusterId = request.ClusterId,
            TotalSuggestions = missionDtos.Count,
            MissionSuggestions = missionDtos
        };
    }

    private static int GetSuggestionPhaseRank(string? phase)
    {
        if (string.IsNullOrWhiteSpace(phase))
            return int.MaxValue;

        return SuggestionPhaseRank.TryGetValue(phase.Trim(), out var rank)
            ? rank
            : int.MaxValue;
    }
}
