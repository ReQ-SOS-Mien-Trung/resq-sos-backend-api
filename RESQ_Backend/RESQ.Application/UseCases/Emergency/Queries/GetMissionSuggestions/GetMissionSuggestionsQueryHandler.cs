using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.UseCases.Emergency.Shared;

namespace RESQ.Application.UseCases.Emergency.Queries.GetMissionSuggestions;

public class GetMissionSuggestionsQueryHandler(
    IMissionAiSuggestionRepository missionRepository,
    ISosClusterRepository sosClusterRepository,
    ILogger<GetMissionSuggestionsQueryHandler> logger
) : IRequestHandler<GetMissionSuggestionsQuery, GetMissionSuggestionsResponse>
{
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

        var suggestions = (await _missionRepository.GetByClusterIdAsync(request.ClusterId, cancellationToken)).ToList();

        var missionDtos = suggestions.Select(m =>
        {
            var metadata = MissionAiSuggestionJsonHelper.ParseMetadata(m.Metadata);

            return new MissionSuggestionDto
            {
                Id = m.Id,
                ClusterId = m.ClusterId,
                ModelName = m.ModelName,
                AnalysisType = m.AnalysisType,
                SuggestedMissionTitle = m.SuggestedMissionTitle,
                SuggestedMissionType = m.SuggestedMissionType,
                SuggestedPriorityScore = m.SuggestedPriorityScore,
                SuggestedSeverityLevel = m.SuggestedSeverityLevel,
                ConfidenceScore = m.ConfidenceScore,
                OverallAssessment = metadata?.OverallAssessment,
                EstimatedDuration = metadata?.EstimatedDuration,
                SpecialNotes = metadata?.SpecialNotes,
                MixedRescueReliefWarning = metadata?.MixedRescueReliefWarning ?? string.Empty,
                NeedsManualReview = metadata?.NeedsManualReview ?? false,
                LowConfidenceWarning = metadata?.LowConfidenceWarning,
                NeedsAdditionalDepot = metadata?.NeedsAdditionalDepot ?? false,
                SupplyShortages = metadata?.SupplyShortages ?? [],
                SuggestedResources = metadata?.SuggestedResources ?? [],
                SuggestionScope = m.SuggestionScope,
                CreatedAt = m.CreatedAt,
                Activities = m.Activities.Select(a => new ActivitySuggestionDto
                {
                    Id = a.Id,
                    ActivityType = a.ActivityType,
                    SuggestionPhase = a.SuggestionPhase,
                    ConfidenceScore = a.ConfidenceScore,
                    CreatedAt = a.CreatedAt,
                    SuggestedActivities = MissionAiSuggestionJsonHelper.ParseActivities(a.SuggestedActivities)
                }).ToList()
            };
        }).ToList();

        return new GetMissionSuggestionsResponse
        {
            ClusterId = request.ClusterId,
            TotalSuggestions = missionDtos.Count,
            MissionSuggestions = missionDtos
        };
    }
}
