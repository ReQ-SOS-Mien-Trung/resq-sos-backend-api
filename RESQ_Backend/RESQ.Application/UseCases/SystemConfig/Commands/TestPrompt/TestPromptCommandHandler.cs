using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.TestPrompt;

public class TestPromptCommandHandler(
    IPromptRepository promptRepository,
    IMissionContextService missionContextService,
    IRescueMissionSuggestionService suggestionService,
    ILogger<TestPromptCommandHandler> logger) : IRequestHandler<TestPromptCommand, TestPromptResponse>
{
    private static readonly HashSet<PromptType> MissionPromptTypes =
    [
        PromptType.MissionPlanning,
        PromptType.MissionRequirementsAssessment,
        PromptType.MissionDepotPlanning,
        PromptType.MissionTeamPlanning,
        PromptType.MissionPlanValidation
    ];

    private readonly IPromptRepository _promptRepository = promptRepository;
    private readonly IMissionContextService _missionContextService = missionContextService;
    private readonly IRescueMissionSuggestionService _suggestionService = suggestionService;
    private readonly ILogger<TestPromptCommandHandler> _logger = logger;

    public async Task<TestPromptResponse> Handle(TestPromptCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Previewing AI mission plan for PromptId={Id}, ClusterId={ClusterId}",
            request.Id,
            request.ClusterId);

        if (request.ClusterId <= 0)
            throw new BadRequestException("ClusterId khong hop le.");

        var prompt = await _promptRepository.GetByIdAsync(request.Id, cancellationToken);
        if (prompt == null)
            throw new NotFoundException($"Khong tim thay prompt voi Id={request.Id}");

        if (!MissionPromptTypes.Contains(prompt.PromptType))
            throw new BadRequestException(
                $"Prompt type '{prompt.PromptType}' khong thuoc luong goi y mission nen khong the preview ke hoach mission.");

        var context = await _missionContextService.PrepareContextAsync(request.ClusterId, cancellationToken);
        var result = await _suggestionService.PreviewSuggestionAsync(
            context.SosRequests,
            context.NearbyDepots,
            context.NearbyTeams,
            context.MultiDepotRecommended,
            request.ClusterId,
            prompt,
            cancellationToken);

        return MapResponse(request, prompt, result, context.SosRequests.Count);
    }

    private static TestPromptResponse MapResponse(
        TestPromptCommand request,
        PromptModel prompt,
        RescueMissionSuggestionResult result,
        int sosRequestCount)
    {
        var model = result.ModelName ?? prompt.Model ?? string.Empty;

        return new TestPromptResponse
        {
            IsSuccess = result.IsSuccess,
            PromptId = prompt.Id,
            PromptName = prompt.Name,
            PromptType = prompt.PromptType,
            ClusterId = request.ClusterId,
            SuggestionId = null,
            Model = model,
            ModelName = model,
            AiResponse = result.RawAiResponse,
            RawAiResponse = result.RawAiResponse,
            ErrorMessage = result.ErrorMessage,
            ResponseTimeMs = (long)result.ResponseTimeMs,
            SosRequestCount = sosRequestCount,
            SuggestedMissionTitle = result.SuggestedMissionTitle,
            SuggestedMissionType = result.SuggestedMissionType,
            SuggestedPriorityScore = result.SuggestedPriorityScore,
            SuggestedSeverityLevel = result.SuggestedSeverityLevel,
            OverallAssessment = result.OverallAssessment,
            SuggestedActivities = result.SuggestedActivities,
            SuggestedResources = result.SuggestedResources,
            EstimatedDuration = result.EstimatedDuration,
            SpecialNotes = result.SpecialNotes,
            NeedsAdditionalDepot = result.NeedsAdditionalDepot,
            SupplyShortages = result.SupplyShortages,
            ConfidenceScore = result.ConfidenceScore,
            NeedsManualReview = result.NeedsManualReview,
            LowConfidenceWarning = result.LowConfidenceWarning,
            MultiDepotRecommended = result.MultiDepotRecommended,
            PipelineMetadata = result.PipelineMetadata
        };
    }
}
