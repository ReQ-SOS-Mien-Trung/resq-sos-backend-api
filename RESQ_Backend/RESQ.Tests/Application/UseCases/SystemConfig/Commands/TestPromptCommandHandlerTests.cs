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
    public async Task Handle_MissionPrompt_PreviewsPlanAndMapsResponse()
    {
        var prompt = BuildPrompt(PromptType.MissionDepotPlanning);
        var contextService = new StubMissionContextService();
        var suggestion = new RescueMissionSuggestionResult
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
        var suggestionService = new StubSuggestionService(suggestion);
        var handler = BuildHandler(prompt, contextService, suggestionService);

        var response = await handler.Handle(new TestPromptCommand(prompt.Id, 7), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Null(response.SuggestionId);
        Assert.Equal(prompt.Id, response.PromptId);
        Assert.Equal(prompt.PromptType, response.PromptType);
        Assert.Equal(7, response.ClusterId);
        Assert.Equal("gemini-preview", response.ModelName);
        Assert.Equal("Preview plan", response.SuggestedMissionTitle);
        Assert.Single(response.SuggestedActivities);
        Assert.Equal("completed", response.PipelineMetadata?.PipelineStatus);
        Assert.Equal(7, contextService.RequestedClusterId);
        Assert.Equal(prompt.Id, suggestionService.PromptOverride?.Id);
        Assert.Equal(7, suggestionService.ClusterId);
    }

    [Fact]
    public async Task Handle_SosPriorityPrompt_RejectsMissionPreview()
    {
        var handler = BuildHandler(BuildPrompt(PromptType.SosPriorityAnalysis));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new TestPromptCommand(1, 7), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MissingPrompt_ThrowsNotFound()
    {
        var handler = new TestPromptCommandHandler(
            new StubPromptRepository(null),
            new StubMissionContextService(),
            new StubSuggestionService(new RescueMissionSuggestionResult()),
            NullLogger<TestPromptCommandHandler>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new TestPromptCommand(999, 7), CancellationToken.None));
    }

    [Fact]
    public void Validator_RejectsMissingClusterId()
    {
        var validator = new TestPromptCommandValidator();

        var result = validator.Validate(new TestPromptCommand(1, 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TestPromptCommand.ClusterId));
    }

    private static TestPromptCommandHandler BuildHandler(
        PromptModel prompt,
        StubMissionContextService? contextService = null,
        StubSuggestionService? suggestionService = null)
    {
        return new TestPromptCommandHandler(
            new StubPromptRepository(prompt),
            contextService ?? new StubMissionContextService(),
            suggestionService ?? new StubSuggestionService(new RescueMissionSuggestionResult { IsSuccess = true }),
            NullLogger<TestPromptCommandHandler>.Instance);
    }

    private static PromptModel BuildPrompt(PromptType promptType) => new()
    {
        Id = 1,
        Name = "Prompt under test",
        PromptType = promptType,
        Model = "gemini-test",
        IsActive = false
    };

    private sealed class StubPromptRepository(PromptModel? prompt) : IPromptRepository
    {
        public Task<PromptModel?> GetActiveByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default)
            => Task.FromResult(prompt?.PromptType == promptType && prompt.IsActive ? prompt : null);

        public Task<PromptModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(prompt?.Id == id ? prompt : null);

        public Task CreateAsync(PromptModel prompt, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(PromptModel prompt, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task DeactivateOthersByTypeAsync(int currentPromptId, PromptType promptType, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

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
