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
            "Previewing AI mission plan for PromptId={Id}, Mode={Mode}, ClusterId={ClusterId}",
            request.Id,
            request.Mode,
            request.ClusterId);

        if (request.ClusterId <= 0)
            throw new BadRequestException("ClusterId khong hop le.");

        var prompt = request.Mode switch
        {
            TestPromptDraftMode.ExistingPromptDraft => await BuildExistingPromptDraftAsync(request, cancellationToken),
            TestPromptDraftMode.NewPromptDraft => BuildNewPromptDraft(request),
            _ => throw new BadRequestException("Che do test prompt khong hop le.")
        };

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

    private async Task<PromptModel> BuildExistingPromptDraftAsync(
        TestPromptCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.Id.HasValue || request.Id.Value <= 0)
            throw new BadRequestException("PromptId khong hop le.");

        var existingPrompt = await _promptRepository.GetByIdAsync(request.Id.Value, cancellationToken);
        if (existingPrompt == null)
            throw new NotFoundException($"Khong tim thay prompt voi Id={request.Id.Value}");

        var draft = ClonePrompt(existingPrompt);
        ApplyDraftFields(draft, request);
        return draft;
    }

    private static PromptModel BuildNewPromptDraft(TestPromptCommand request)
    {
        return new PromptModel
        {
            Name = request.Name ?? string.Empty,
            PromptType = request.PromptType ?? default,
            Provider = request.Provider ?? AiProvider.Gemini,
            Purpose = request.Purpose,
            SystemPrompt = request.SystemPrompt,
            UserPromptTemplate = request.UserPromptTemplate,
            Model = request.Model,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Version = request.Version,
            ApiUrl = request.ApiUrl,
            ApiKey = request.ApiKey,
            IsActive = request.IsActive ?? true
        };
    }

    private static PromptModel ClonePrompt(PromptModel source)
    {
        return new PromptModel
        {
            Id = source.Id,
            Name = source.Name,
            PromptType = source.PromptType,
            Provider = source.Provider,
            Purpose = source.Purpose,
            SystemPrompt = source.SystemPrompt,
            UserPromptTemplate = source.UserPromptTemplate,
            Model = source.Model,
            Temperature = source.Temperature,
            MaxTokens = source.MaxTokens,
            Version = source.Version,
            ApiUrl = source.ApiUrl,
            ApiKey = source.ApiKey,
            IsActive = source.IsActive,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };
    }

    private static void ApplyDraftFields(PromptModel draft, TestPromptCommand request)
    {
        if (request.Name != null) draft.Name = request.Name;
        if (request.PromptType.HasValue) draft.PromptType = request.PromptType.Value;
        if (request.Provider.HasValue) draft.Provider = request.Provider.Value;
        if (request.Purpose != null) draft.Purpose = request.Purpose;
        if (request.SystemPrompt != null) draft.SystemPrompt = request.SystemPrompt;
        if (request.UserPromptTemplate != null) draft.UserPromptTemplate = request.UserPromptTemplate;
        if (request.Model != null) draft.Model = request.Model;
        if (request.Temperature.HasValue) draft.Temperature = request.Temperature.Value;
        if (request.MaxTokens.HasValue) draft.MaxTokens = request.MaxTokens.Value;
        if (request.Version != null) draft.Version = request.Version;
        if (request.ApiUrl != null) draft.ApiUrl = request.ApiUrl;
        if (request.ApiKey != null) draft.ApiKey = request.ApiKey;
        if (request.IsActive.HasValue) draft.IsActive = request.IsActive.Value;
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
            PromptId = request.Mode == TestPromptDraftMode.NewPromptDraft ? null : prompt.Id,
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
