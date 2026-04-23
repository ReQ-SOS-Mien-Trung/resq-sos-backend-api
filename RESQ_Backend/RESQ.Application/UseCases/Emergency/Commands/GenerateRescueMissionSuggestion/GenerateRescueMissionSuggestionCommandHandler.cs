using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Emergency.Commands.GenerateRescueMissionSuggestion;

public class GenerateRescueMissionSuggestionCommandHandler(
    ISosClusterRepository sosClusterRepository,
    IMissionContextService missionContextService,
    IRescueMissionSuggestionService suggestionService,
    IUnitOfWork unitOfWork,
    ILogger<GenerateRescueMissionSuggestionCommandHandler> logger
) : IRequestHandler<GenerateRescueMissionSuggestionCommand, GenerateRescueMissionSuggestionResponse>
{
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly IMissionContextService _missionContextService = missionContextService;
    private readonly IRescueMissionSuggestionService _suggestionService = suggestionService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<GenerateRescueMissionSuggestionCommandHandler> _logger = logger;

    public async Task<GenerateRescueMissionSuggestionResponse> Handle(
        GenerateRescueMissionSuggestionCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Generating rescue mission suggestion for ClusterId={clusterId}, RequestedBy={userId}",
            request.ClusterId,
            request.RequestedByUserId);

        var context = await _missionContextService.PrepareContextAsync(request.ClusterId, cancellationToken);

        var result = await _suggestionService.GenerateSuggestionAsync(
            context.SosRequests,
            context.NearbyDepots,
            context.NearbyTeams,
            context.MultiDepotRecommended,
            request.ClusterId,
            cancellationToken);

        if (result.IsSuccess &&
            result.SuggestionId.HasValue &&
            context.Cluster.Status == SosClusterStatus.Pending)
        {
            context.Cluster.Status = SosClusterStatus.Suggested;
            await _sosClusterRepository.UpdateAsync(context.Cluster, cancellationToken);
            await _unitOfWork.SaveAsync();
        }

        return new GenerateRescueMissionSuggestionResponse
        {
            SuggestionId = result.SuggestionId,
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            ModelName = result.ModelName,
            ResponseTimeMs = result.ResponseTimeMs,
            SosRequestCount = context.SosRequests.Count,
            SuggestedMissionTitle = result.SuggestedMissionTitle,
            SuggestedMissionType = result.SuggestedMissionType,
            SuggestedPriorityScore = result.SuggestedPriorityScore,
            SuggestedSeverityLevel = result.SuggestedSeverityLevel,
            OverallAssessment = result.OverallAssessment,
            SuggestedActivities = result.SuggestedActivities,
            SuggestedResources = result.SuggestedResources,
            EstimatedDuration = result.EstimatedDuration,
            SpecialNotes = result.SpecialNotes,
            MixedRescueReliefWarning = result.MixedRescueReliefWarning,
            NeedsAdditionalDepot = result.NeedsAdditionalDepot,
            SupplyShortages = result.SupplyShortages,
            ConfidenceScore = result.ConfidenceScore,
            NeedsManualReview = result.NeedsManualReview,
            LowConfidenceWarning = result.LowConfidenceWarning,
            MultiDepotRecommended = result.MultiDepotRecommended,
            PipelineExecutionMode = result.PipelineMetadata?.ExecutionMode,
            PipelineStatus = result.PipelineMetadata?.PipelineStatus,
            PipelineFinalResultSource = result.PipelineMetadata?.FinalResultSource,
            PipelineFailedStage = result.PipelineMetadata?.FailedStage,
            PipelineFailureReason = result.PipelineMetadata?.FailureReason,
            PipelineUsedLegacyFallback = result.PipelineMetadata?.UsedLegacyFallback
        };
    }
}
