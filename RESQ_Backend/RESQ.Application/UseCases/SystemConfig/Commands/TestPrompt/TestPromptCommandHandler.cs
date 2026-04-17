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
    IAiConfigRepository aiConfigRepository,
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
    private readonly IAiConfigRepository _aiConfigRepository = aiConfigRepository;
    private readonly IMissionContextService _missionContextService = missionContextService;
    private readonly IRescueMissionSuggestionService _suggestionService = suggestionService;
    private readonly ILogger<TestPromptCommandHandler> _logger = logger;

    public async Task<TestPromptResponse> Handle(TestPromptCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Previewing AI mission plan for PromptId={Id}, Mode={Mode}, ClusterId={ClusterId}, AiConfigId={AiConfigId}",
            request.Id,
            request.Mode,
            request.ClusterId,
            request.AiConfigId);

        if (request.ClusterId <= 0)
            throw new BadRequestException("ClusterId không hợp lệ.");

        var prompt = request.Mode switch
        {
            TestPromptDraftMode.ExistingPromptDraft => await BuildExistingPromptDraftAsync(request, cancellationToken),
            TestPromptDraftMode.NewPromptDraft => BuildNewPromptDraft(request),
            _ => throw new BadRequestException("Chế độ test prompt không hợp lệ.")
        };

        if (!MissionPromptTypes.Contains(prompt.PromptType))
        {
            throw new BadRequestException(
                $"Prompt type '{prompt.PromptType}' không thuộc luồng gợi ý mission nên không thể preview kế hoạch mission.");
        }

        var aiConfig = await ResolveAiConfigAsync(request, cancellationToken);
        var context = await _missionContextService.PrepareContextAsync(request.ClusterId, cancellationToken);
        var result = await _suggestionService.PreviewSuggestionAsync(
            context.SosRequests,
            context.NearbyDepots,
            context.NearbyTeams,
            context.MultiDepotRecommended,
            request.ClusterId,
            prompt,
            aiConfig,
            cancellationToken);

        return MapResponse(request, prompt, aiConfig, result, context.SosRequests.Count);
    }

    private async Task<PromptModel> BuildExistingPromptDraftAsync(
        TestPromptCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.Id.HasValue || request.Id.Value <= 0)
            throw new BadRequestException("PromptId không hợp lệ.");

        var existingPrompt = await _promptRepository.GetByIdAsync(request.Id.Value, cancellationToken);
        if (existingPrompt == null)
            throw new NotFoundException($"Không tìm thấy prompt với Id={request.Id.Value}");

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
            Purpose = request.Purpose,
            SystemPrompt = request.SystemPrompt,
            UserPromptTemplate = request.UserPromptTemplate,
            Version = request.Version,
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
            Purpose = source.Purpose,
            SystemPrompt = source.SystemPrompt,
            UserPromptTemplate = source.UserPromptTemplate,
            Version = source.Version,
            IsActive = source.IsActive,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };
    }

    private static void ApplyDraftFields(PromptModel draft, TestPromptCommand request)
    {
        if (request.Name != null) draft.Name = request.Name;
        if (request.PromptType.HasValue) draft.PromptType = request.PromptType.Value;
        if (request.Purpose != null) draft.Purpose = request.Purpose;
        if (request.SystemPrompt != null) draft.SystemPrompt = request.SystemPrompt;
        if (request.UserPromptTemplate != null) draft.UserPromptTemplate = request.UserPromptTemplate;
        if (request.Version != null) draft.Version = request.Version;
        if (request.IsActive.HasValue) draft.IsActive = request.IsActive.Value;
    }

    private async Task<AiConfigModel> ResolveAiConfigAsync(
        TestPromptCommand request,
        CancellationToken cancellationToken)
    {
        if (request.AiConfigId.HasValue)
        {
            return await _aiConfigRepository.GetByIdAsync(request.AiConfigId.Value, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy AI config với Id={request.AiConfigId.Value}");
        }

        return await _aiConfigRepository.GetActiveAsync(cancellationToken)
            ?? throw new BadRequestException("Chưa có AI config active trong hệ thống.");
    }

    private static TestPromptResponse MapResponse(
        TestPromptCommand request,
        PromptModel prompt,
        AiConfigModel aiConfig,
        RescueMissionSuggestionResult result,
        int sosRequestCount)
    {
        var runtimeModelName = result.ModelName ?? aiConfig.Model;

        return new TestPromptResponse
        {
            IsSuccess = result.IsSuccess,
            PromptId = request.Mode == TestPromptDraftMode.NewPromptDraft ? null : prompt.Id,
            PromptName = prompt.Name,
            PromptType = prompt.PromptType,
            ClusterId = request.ClusterId,
            SuggestionId = null,
            AiConfigId = aiConfig.Id,
            AiConfigVersion = aiConfig.Version,
            Provider = aiConfig.Provider,
            Model = aiConfig.Model,
            Temperature = aiConfig.Temperature,
            MaxTokens = aiConfig.MaxTokens,
            ModelName = runtimeModelName,
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
