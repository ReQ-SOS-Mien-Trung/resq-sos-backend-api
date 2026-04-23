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
        var storedPrompt = BuildPrompt(PromptType.MissionTeamPlanning);
        var promptRepository = new StubPromptRepository(storedPrompt);
        var aiConfigRepository = new StubAiConfigRepository(BuildAiConfig());
        var contextService = new StubMissionContextService();
        var suggestionService = new StubSuggestionService(BuildSuggestion());
        var handler = BuildHandler(promptRepository, aiConfigRepository, contextService, suggestionService);

        var response = await handler.Handle(new TestPromptCommand(
            storedPrompt.Id,
            TestPromptDraftMode.ExistingPromptDraft,
            7,
            Name: null,
            PromptType: PromptType.MissionDepotPlanning,
            Purpose: null,
            SystemPrompt: "draft system",
            UserPromptTemplate: "draft user",
            Version: null,
            IsActive: false,
            AiConfigId: null), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(storedPrompt.Id, response.PromptId);
        Assert.Equal(7, response.ClusterId);
        Assert.Equal("Preview plan", response.SuggestedMissionTitle);
        Assert.Equal(aiConfigRepository.ActiveConfig?.Id, response.AiConfigId);
        Assert.Equal("warning riêng", response.MixedRescueReliefWarning);
        Assert.NotSame(storedPrompt, suggestionService.PromptOverride);
        Assert.Equal(PromptType.MissionDepotPlanning, suggestionService.PromptOverride?.PromptType);
        Assert.Equal("draft system", suggestionService.PromptOverride?.SystemPrompt);
        Assert.Equal("draft user", suggestionService.PromptOverride?.UserPromptTemplate);
        Assert.False(suggestionService.PromptOverride?.IsActive);
        Assert.Equal(PromptType.MissionTeamPlanning, storedPrompt.PromptType);
        Assert.Equal("stored system", storedPrompt.SystemPrompt);
        Assert.Equal("stored user", storedPrompt.UserPromptTemplate);
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
        var handler = BuildHandler(
            new StubPromptRepository(storedPrompt),
            new StubAiConfigRepository(BuildAiConfig()),
            suggestionService: suggestionService);

        await handler.Handle(new TestPromptCommand(
            storedPrompt.Id,
            TestPromptDraftMode.ExistingPromptDraft,
            7,
            Name: null,
            PromptType: null,
            Purpose: null,
            SystemPrompt: null,
            UserPromptTemplate: null,
            Version: null,
            IsActive: null,
            AiConfigId: null), CancellationToken.None);

        Assert.Equal(storedPrompt.Id, suggestionService.PromptOverride?.Id);
        Assert.Equal(storedPrompt.Name, suggestionService.PromptOverride?.Name);
        Assert.Equal(storedPrompt.PromptType, suggestionService.PromptOverride?.PromptType);
        Assert.Equal(storedPrompt.SystemPrompt, suggestionService.PromptOverride?.SystemPrompt);
        Assert.Equal(storedPrompt.UserPromptTemplate, suggestionService.PromptOverride?.UserPromptTemplate);
    }

    [Fact]
    public async Task Handle_NewPromptDraft_BuildsInMemoryPromptAndReturnsNullPromptId()
    {
        var suggestionService = new StubSuggestionService(BuildSuggestion());
        var aiConfig = BuildAiConfig(id: 9, model: "test-model");
        var handler = BuildHandler(
            new StubPromptRepository(null),
            new StubAiConfigRepository(aiConfig),
            suggestionService: suggestionService);

        var response = await handler.Handle(BuildNewPromptCommand(), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Null(response.PromptId);
        Assert.Equal(0, suggestionService.PromptOverride?.Id);
        Assert.Equal("Draft create prompt", suggestionService.PromptOverride?.Name);
        Assert.Equal(PromptType.MissionPlanValidation, suggestionService.PromptOverride?.PromptType);
        Assert.Equal("new system", suggestionService.PromptOverride?.SystemPrompt);
        Assert.Equal("new user", suggestionService.PromptOverride?.UserPromptTemplate);
        Assert.Equal(aiConfig.Id, response.AiConfigId);
        Assert.Equal(aiConfig.Model, response.Model);
    }

    [Fact]
    public async Task Handle_ExplicitAiConfigId_UsesRequestedVersion()
    {
        var aiConfig = BuildAiConfig(id: 99, model: "override-model");
        var handler = BuildHandler(
            new StubPromptRepository(BuildPrompt(PromptType.MissionTeamPlanning)),
            new StubAiConfigRepository(aiConfig),
            suggestionService: new StubSuggestionService(BuildSuggestion()));

        var response = await handler.Handle(new TestPromptCommand(
            1,
            TestPromptDraftMode.ExistingPromptDraft,
            7,
            Name: null,
            PromptType: null,
            Purpose: null,
            SystemPrompt: null,
            UserPromptTemplate: null,
            Version: null,
            IsActive: null,
            AiConfigId: 99), CancellationToken.None);

        Assert.Equal(99, response.AiConfigId);
        Assert.Equal("override-model", response.Model);
    }

    [Fact]
    public async Task Handle_DraftPromptTypeSosPriority_RejectsMissionPreview()
    {
        var storedPrompt = BuildPrompt(PromptType.MissionTeamPlanning);
        var handler = BuildHandler(
            new StubPromptRepository(storedPrompt),
            new StubAiConfigRepository(BuildAiConfig()));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new TestPromptCommand(
                storedPrompt.Id,
                TestPromptDraftMode.ExistingPromptDraft,
                7,
                Name: null,
                PromptType: PromptType.SosPriorityAnalysis,
                Purpose: null,
                SystemPrompt: null,
                UserPromptTemplate: null,
                Version: null,
                IsActive: null,
                AiConfigId: null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MissingExistingPrompt_ThrowsNotFound()
    {
        var handler = BuildHandler(
            new StubPromptRepository(null),
            new StubAiConfigRepository(BuildAiConfig()));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(BuildExistingPromptCommand(999, 7), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MissingAiConfig_ThrowsClearError()
    {
        var handler = BuildHandler(
            new StubPromptRepository(BuildPrompt(PromptType.MissionTeamPlanning)),
            new StubAiConfigRepository(null));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(BuildExistingPromptCommand(1, 7), CancellationToken.None));
    }

    [Fact]
    public void Validator_RejectsInvalidClusterAndAiConfigId()
    {
        var validator = new TestPromptCommandValidator();

        var result = validator.Validate(new TestPromptCommand(
            1,
            TestPromptDraftMode.ExistingPromptDraft,
            0,
            Name: null,
            PromptType: null,
            Purpose: null,
            SystemPrompt: null,
            UserPromptTemplate: null,
            Version: null,
            IsActive: null,
            AiConfigId: 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TestPromptCommand.ClusterId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TestPromptCommand.AiConfigId));
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
            Purpose: null,
            SystemPrompt: null,
            UserPromptTemplate: null,
            Version: null,
            IsActive: true,
            AiConfigId: null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TestPromptCommand.Name));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TestPromptCommand.PromptType));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TestPromptCommand.SystemPrompt));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TestPromptCommand.UserPromptTemplate));
    }

    private static TestPromptCommandHandler BuildHandler(
        StubPromptRepository promptRepository,
        StubAiConfigRepository aiConfigRepository,
        StubMissionContextService? contextService = null,
        StubSuggestionService? suggestionService = null)
    {
        return new TestPromptCommandHandler(
            promptRepository,
            aiConfigRepository,
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
        Purpose: null,
        SystemPrompt: null,
        UserPromptTemplate: null,
        Version: null,
        IsActive: null,
        AiConfigId: null);

    private static TestPromptCommand BuildNewPromptCommand() => new(
        null,
        TestPromptDraftMode.NewPromptDraft,
        7,
        Name: "Draft create prompt",
        PromptType: PromptType.MissionPlanValidation,
        Purpose: "Preview new prompt",
        SystemPrompt: "new system",
        UserPromptTemplate: "new user",
        Version: "1.0",
        IsActive: true,
        AiConfigId: null);

    private static PromptModel BuildPrompt(PromptType promptType) => new()
    {
        Id = 1,
        Name = "Prompt under test",
        PromptType = promptType,
        Purpose = "Stored purpose",
        SystemPrompt = "stored system",
        UserPromptTemplate = "stored user",
        Version = "stored-version",
        IsActive = true
    };

    private static AiConfigModel BuildAiConfig(int id = 1, string model = "gemini-preview") => new()
    {
        Id = id,
        Name = "AI config under test",
        Provider = AiProvider.Gemini,
        Model = model,
        Temperature = 0.3,
        MaxTokens = 2048,
        ApiUrl = "https://example.test/{0}/{1}",
        ApiKey = "test-key",
        Version = "v1.0",
        IsActive = true
    };

    private static RescueMissionSuggestionResult BuildSuggestion() => new()
    {
        IsSuccess = true,
        SuggestionId = 99,
        ModelName = "gemini-preview",
        SuggestedMissionTitle = "Preview plan",
        SuggestedMissionType = "MIXED",
        SuggestedPriorityScore = 8.2,
        RawAiResponse = "{\"mission_title\":\"Preview plan\"}",
        MixedRescueReliefWarning = "warning riêng",
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

        public Task<bool> ExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
        {
            ExistsCalls++;
            return Task.FromResult(false);
        }

        public Task<bool> ExistsVersionAsync(PromptType promptType, string version, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<PromptModel>> GetVersionsByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PromptModel>>([]);

        public Task DeactivateOthersByTypeAsync(int currentPromptId, PromptType promptType, CancellationToken cancellationToken = default)
        {
            DeactivateOthersCalls++;
            return Task.CompletedTask;
        }

        public Task<PagedResult<PromptModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubAiConfigRepository(AiConfigModel? activeConfig) : IAiConfigRepository
    {
        public AiConfigModel? ActiveConfig { get; } = activeConfig;

        public Task<AiConfigModel?> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ActiveConfig);

        public Task<AiConfigModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(ActiveConfig?.Id == id ? ActiveConfig : null);

        public Task CreateAsync(AiConfigModel config, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(AiConfigModel config, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsVersionAsync(string version, int? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<AiConfigModel>> GetVersionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AiConfigModel>>(ActiveConfig is null ? [] : [ActiveConfig]);
        public Task DeactivateOthersAsync(int currentConfigId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PagedResult<AiConfigModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
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
        public AiConfigModel? AiConfigOverride { get; private set; }

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
            AiConfigModel? aiConfigOverride = null,
            CancellationToken cancellationToken = default)
        {
            ClusterId = clusterId;
            PromptOverride = promptOverride;
            AiConfigOverride = aiConfigOverride;
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
