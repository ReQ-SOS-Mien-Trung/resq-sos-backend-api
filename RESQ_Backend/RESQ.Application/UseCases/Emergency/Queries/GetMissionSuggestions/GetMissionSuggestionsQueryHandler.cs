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
            var pipeline = metadata?.Pipeline;

            // Pick the Validated phase if exists, otherwise fall back to the latest activity group
            var validatedActivity = m.Activities
                .Where(a => a.SuggestionPhase == "Validated")
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefault();

            var bestActivity = validatedActivity
                ?? m.Activities.OrderByDescending(a => a.CreatedAt).FirstOrDefault();

            var suggestedActivities = bestActivity != null
                ? MissionAiSuggestionJsonHelper.ParseActivities(bestActivity.SuggestedActivities)
                : [];

            var confidenceScore = bestActivity?.ConfidenceScore ?? m.ConfidenceScore;
            var isSuccess = metadata?.IsSuccess
                ?? !string.Equals(pipeline?.PipelineStatus, "failed", StringComparison.OrdinalIgnoreCase);
            var errorMessage = metadata?.ErrorMessage;

            var mixedRescueReliefWarning = MissionSuggestionWarningHelper.ResolveMixedRescueReliefWarning(
                suggestedActivities,
                metadata?.MixedRescueReliefWarning);

            // Count distinct SOS requests from activities
            var sosRequestCount = suggestedActivities
                .Where(a => a.SosRequestId.HasValue)
                .Select(a => a.SosRequestId!.Value)
                .Distinct()
                .Count();

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
                ConfidenceScore = confidenceScore,
                IsSuccess = isSuccess,
                ErrorMessage = errorMessage,
                OverallAssessment = metadata?.OverallAssessment,
                EstimatedDuration = metadata?.EstimatedDuration,
                SpecialNotes = metadata?.SpecialNotes,
                MixedRescueReliefWarning = mixedRescueReliefWarning,
                NeedsManualReview = (metadata?.NeedsManualReview ?? false) || !string.IsNullOrWhiteSpace(mixedRescueReliefWarning),
                LowConfidenceWarning = metadata?.LowConfidenceWarning,
                NeedsAdditionalDepot = metadata?.NeedsAdditionalDepot ?? false,
                SupplyShortages = metadata?.SupplyShortages ?? [],
                SuggestedResources = metadata?.SuggestedResources ?? [],
                PipelineStatus = pipeline?.PipelineStatus,
                PipelineFinalResultSource = pipeline?.FinalResultSource,
                PipelineFailedStage = pipeline?.FailedStage,
                PipelineFailureReason = pipeline?.FailureReason,
                SuggestionScope = m.SuggestionScope,
                CreatedAt = m.CreatedAt,
                SuggestedActivities = suggestedActivities,
                SosRequestCount = sosRequestCount
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
