using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.UseCases.SystemConfig.Commands.TestPrompt;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;

namespace RESQ.Tests.Application.UseCases.SystemConfig.Commands;

public class TestPromptCommandHandlerTests
{
    [Fact]
    public async Task Handle_ExistingPromptDraft_MergesDraftFieldsWithoutMutatingStoredPrompt()
    {
        var storedPrompt = BuildPrompt(PromptType.MissionPlanning);
        var promptRepository = new StubPromptRepository(storedPrompt);
        var contextService = new StubMissionContextService();
        var suggestionService = new StubSuggestionService(BuildSuggestion());
        var handler = BuildHandler(promptRepository, contextService, suggestionService);

        var response = await handler.Handle(new TestPromptCommand(
            storedPrompt.Id,
            TestPromptDraftMode.ExistingPromptDraft,
            7,
            Name: null,
            PromptType: PromptType.MissionDepotPlanning,
            Provider: null,
            Purpose: null,
            SystemPrompt: "draft system",
            UserPromptTemplate: "draft user",
            Model: "draft-model",
            Temperature: 0.7,
            MaxTokens: null,
            Version: null,
            ApiUrl: null,
            ApiKey: null,
            IsActive: false), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(storedPrompt.Id, response.PromptId);
        Assert.Equal(7, response.ClusterId);
        Assert.Equal("Preview plan", response.SuggestedMissionTitle);
        Assert.NotSame(storedPrompt, suggestionService.PromptOverride);
        Assert.Equal(PromptType.MissionDepotPlanning, suggestionService.PromptOverride?.PromptType);
        Assert.Equal("draft system", suggestionService.PromptOverride?.SystemPrompt);
        Assert.Equal("draft user", suggestionService.PromptOverride?.UserPromptTemplate);
        Assert.Equal("draft-model", suggestionService.PromptOverride?.Model);
        Assert.Equal(0.7, suggestionService.PromptOverride?.Temperature);
        Assert.False(suggestionService.PromptOverride?.IsActive);
        Assert.Equal(PromptType.MissionPlanning, storedPrompt.PromptType);
        Assert.Equal("stored system", storedPrompt.SystemPrompt);
        Assert.Equal("stored user", storedPrompt.UserPromptTemplate);
        Assert.Equal("stored-model", storedPrompt.Model);
        Assert.Equal(0, promptRepository.CreateCalls);
        Assert.Equal(0, promptRepository.UpdateCalls);
        Assert.Equal(0, promptRepository.DeactivateOthersCalls);
        Assert.Equal(0, promptRepository.ExistsCalls);
    }

    [Fact]
    public async Task Handle_ExistingPromptDraft_UsesStoredValuesWhenFieldsAreMissing()
    {
        var storedPrompt = BuildPrompt(PromptType.MissionTeamPlanning);
        var suggestionService = new StubSuggestionService(BuildSuggestion());
        var handler = BuildHandler(new StubPromptRepository(storedPrompt), suggestionService: suggestionService);

        await handler.Handle(new TestPromptCommand(
            storedPrompt.Id,
            TestPromptDraftMode.ExistingPromptDraft,
            7,
            Name: null,
            PromptType: null,
            Provider: null,
            Purpose: null,
            SystemPrompt: null,
            UserPromptTemplate: null,
            Model: null,
            Temperature: null,
            MaxTokens: null,
            Version: null,
            ApiUrl: null,
            ApiKey: null,
            IsActive: null), CancellationToken.None);

        Assert.Equal(storedPrompt.Id, suggestionService.PromptOverride?.Id);
        Assert.Equal(storedPrompt.Name, suggestionService.PromptOverride?.Name);
        Assert.Equal(storedPrompt.PromptType, suggestionService.PromptOverride?.PromptType);
        Assert.Equal(storedPrompt.SystemPrompt, suggestionService.PromptOverride?.SystemPrompt);
        Assert.Equal(storedPrompt.UserPromptTemplate, suggestionService.PromptOverride?.UserPromptTemplate);
        Assert.Equal(storedPrompt.Model, suggestionService.PromptOverride?.Model);
    }

    [Fact]
    public async Task Handle_NewPromptDraft_BuildsInMemoryPromptAndReturnsNullPromptId()
    {
        var suggestionService = new StubSuggestionService(BuildSuggestion());
        var handler = BuildHandler(new StubPromptRepository(null), suggestionService: suggestionService);

        var response = await handler.Handle(BuildNewPromptCommand(), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Null(response.PromptId);
        Assert.Equal(0, suggestionService.PromptOverride?.Id);
        Assert.Equal("Draft create prompt", suggestionService.PromptOverride?.Name);
        Assert.Equal(PromptType.MissionPlanValidation, suggestionService.PromptOverride?.PromptType);
        Assert.Equal(AiProvider.Gemini, suggestionService.PromptOverride?.Provider);
        Assert.Equal("new system", suggestionService.PromptOverride?.SystemPrompt);
        Assert.Equal("new user", suggestionService.PromptOverride?.UserPromptTemplate);
        Assert.Equal("new-model", suggestionService.PromptOverride?.Model);
    }

    [Fact]
    public async Task Handle_DraftPromptTypeSosPriority_RejectsMissionPreview()
    {
        var storedPrompt = BuildPrompt(PromptType.MissionPlanning);
        var handler = BuildHandler(new StubPromptRepository(storedPrompt));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new TestPromptCommand(
                storedPrompt.Id,
                TestPromptDraftMode.ExistingPromptDraft,
                7,
                Name: null,
                PromptType: PromptType.SosPriorityAnalysis,
                Provider: null,
                Purpose: null,
                SystemPrompt: null,
                UserPromptTemplate: null,
                Model: null,
                Temperature: null,
                MaxTokens: null,
                Version: null,
                ApiUrl: null,
                ApiKey: null,
                IsActive: null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MissingExistingPrompt_ThrowsNotFound()
    {
        var handler = BuildHandler(new StubPromptRepository(null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(BuildExistingPromptCommand(999, 7), CancellationToken.None));
    }

    [Fact]
    public void Validator_RejectsInvalidClusterAndPromptFields()
    {
        var validator = new TestPromptCommandValidator();

        var result = validator.Validate(new TestPromptCommand(
            1,
            TestPromptDraftMode.ExistingPromptDraft,
            0,
            Name: null,
            PromptType: null,
            Provider: null,
            Purpose: null,
            SystemPrompt: null,
            UserPromptTemplate: null,
            Model: null,
            Temperature: 3,
            MaxTokens: 0,
            Version: null,
            ApiUrl: null,
            ApiKey: null,
            IsActive: null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TestPromptCommand.ClusterId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TestPromptCommand.Temperature));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TestPromptCommand.MaxTokens));
    }

    [Fact]
    public void Validator_RejectsNewPromptDraftMissingRequiredFields()
    {
        var validator = new TestPromptCommandValidator();

        var result = validator.Validate(new TestPromptCommand(
            null,
            TestPromptDraftMode.NewPromptDraft,
            7,
            Name: null,
            PromptType: null,
            Provider: (AiProvider)999,
            Purpose: null,
            SystemPrompt: null,
            UserPromptTemplate: null,
            Model: null,
            Temperature: null,
            MaxTokens: null,
            Version: null,
            ApiUrl: null,
            ApiKey: null,
            IsActive: true));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TestPromptCommand.Name));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TestPromptCommand.PromptType));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TestPromptCommand.Provider));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TestPromptCommand.SystemPrompt));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TestPromptCommand.UserPromptTemplate));
    }

    private static TestPromptCommandHandler BuildHandler(
        StubPromptRepository promptRepository,
        StubMissionContextService? contextService = null,
        StubSuggestionService? suggestionService = null)
    {
        return new TestPromptCommandHandler(
            promptRepository,
            contextService ?? new StubMissionContextService(),
            suggestionService ?? new StubSuggestionService(BuildSuggestion()),
            NullLogger<TestPromptCommandHandler>.Instance);
    }

    private static TestPromptCommand BuildExistingPromptCommand(int id, int clusterId) => new(
        id,
        TestPromptDraftMode.ExistingPromptDraft,
        clusterId,
        Name: null,
        PromptType: null,
        Provider: null,
        Purpose: null,
        SystemPrompt: null,
        UserPromptTemplate: null,
        Model: null,
        Temperature: null,
        MaxTokens: null,
        Version: null,
        ApiUrl: null,
        ApiKey: null,
        IsActive: null);

    private static TestPromptCommand BuildNewPromptCommand() => new(
        null,
        TestPromptDraftMode.NewPromptDraft,
        7,
        Name: "Draft create prompt",
        PromptType: PromptType.MissionPlanValidation,
        Provider: AiProvider.Gemini,
        Purpose: "Preview new prompt",
        SystemPrompt: "new system",
        UserPromptTemplate: "new user",
        Model: "new-model",
        Temperature: 0.3,
        MaxTokens: 2048,
        Version: "1.0",
        ApiUrl: null,
        ApiKey: null,
        IsActive: true);

    private static PromptModel BuildPrompt(PromptType promptType) => new()
    {
        Id = 1,
        Name = "Prompt under test",
        PromptType = promptType,
        Provider = AiProvider.OpenRouter,
        Purpose = "Stored purpose",
        SystemPrompt = "stored system",
        UserPromptTemplate = "stored user",
        Model = "stored-model",
        Temperature = 0.2,
        MaxTokens = 4096,
        Version = "stored-version",
        ApiUrl = "https://stored.example",
        ApiKey = "stored-key",
        IsActive = true
    };

    private static RescueMissionSuggestionResult BuildSuggestion() => new()
    {
        IsSuccess = true,
        SuggestionId = 99,
        ModelName = "gemini-preview",
        SuggestedMissionTitle = "Preview plan",
        SuggestedMissionType = "MIXED",
        ConfidenceScore = 0.82,
        RawAiResponse = "{\"mission_title\":\"Preview plan\"}",
        PipelineMetadata = new MissionSuggestionPipelineMetadata
        {
            ExecutionMode = "pipeline",
            PipelineStatus = "completed",
            FinalResultSource = "validated"
        },
        SuggestedActivities =
        [
            new SuggestedActivityDto
            {
                Step = 1,
                ActivityType = "RESCUE",
                Description = "Rescue victims"
            }
        ]
    };

    private sealed class StubPromptRepository(PromptModel? prompt) : IPromptRepository
    {
        public int CreateCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public int DeactivateOthersCalls { get; private set; }
        public int ExistsCalls { get; private set; }

        public Task<PromptModel?> GetActiveByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default)
            => Task.FromResult(prompt?.PromptType == promptType && prompt.IsActive ? prompt : null);

        public Task<PromptModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(prompt?.Id == id ? prompt : null);

        public Task CreateAsync(PromptModel prompt, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PromptModel prompt, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
        {
            ExistsCalls++;
            return Task.FromResult(false);
        }

        public Task DeactivateOthersByTypeAsync(int currentPromptId, PromptType promptType, CancellationToken cancellationToken = default)
        {
            DeactivateOthersCalls++;
            return Task.CompletedTask;
        }

        public Task<PagedResult<PromptModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubMissionContextService : IMissionContextService
    {
        public int RequestedClusterId { get; private set; }

        public Task<MissionContext> PrepareContextAsync(int clusterId, CancellationToken cancellationToken = default)
        {
            RequestedClusterId = clusterId;
            return Task.FromResult(new MissionContext
            {
                Cluster = new SosClusterModel { Id = clusterId },
                SosRequests = [new SosRequestSummary { Id = 12, RawMessage = "Need help" }],
                NearbyDepots = [],
                NearbyTeams = [],
                MultiDepotRecommended = false
            });
        }
    }

    private sealed class StubSuggestionService(RescueMissionSuggestionResult result) : IRescueMissionSuggestionService
    {
        public int? ClusterId { get; private set; }
        public PromptModel? PromptOverride { get; private set; }

        public Task<RescueMissionSuggestionResult> GenerateSuggestionAsync(
            List<SosRequestSummary> sosRequests,
            List<DepotSummary>? nearbyDepots = null,
            List<AgentTeamInfo>? nearbyTeams = null,
            bool isMultiDepotRecommended = false,
            int? clusterId = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RescueMissionSuggestionResult> PreviewSuggestionAsync(
            List<SosRequestSummary> sosRequests,
            List<DepotSummary>? nearbyDepots,
            List<AgentTeamInfo>? nearbyTeams,
            bool isMultiDepotRecommended,
            int clusterId,
            PromptModel promptOverride,
            CancellationToken cancellationToken = default)
        {
            ClusterId = clusterId;
            PromptOverride = promptOverride;
            return Task.FromResult(result);
        }

        public IAsyncEnumerable<SseMissionEvent> GenerateSuggestionStreamAsync(
            List<SosRequestSummary> sosRequests,
            List<DepotSummary>? nearbyDepots = null,
            List<AgentTeamInfo>? nearbyTeams = null,
            bool isMultiDepotRecommended = false,
            int? clusterId = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
