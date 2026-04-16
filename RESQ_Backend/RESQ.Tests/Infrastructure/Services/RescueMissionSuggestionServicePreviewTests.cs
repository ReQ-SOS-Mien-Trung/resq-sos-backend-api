using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;
using RESQ.Infrastructure.Options;
using RESQ.Infrastructure.Services;

namespace RESQ.Tests.Infrastructure.Services;

public class RescueMissionSuggestionServicePreviewTests
{
    [Fact]
    public async Task PreviewSuggestionAsync_MissionPlanningPrompt_DoesNotPersistSuggestion()
    {
        var suggestionRepository = new RecordingMissionAiSuggestionRepository();
        var service = new RescueMissionSuggestionService(
            new StubAiProviderClientFactory(new StubAiProviderClient()),
            new StubSettingsResolver(),
            ThrowingProxy<IPromptRepository>.Create(),
            suggestionRepository,
            ThrowingProxy<IDepotInventoryRepository>.Create(),
            ThrowingProxy<IItemModelMetadataRepository>.Create(),
            new EmptyAssemblyPointRepository(),
            Options.Create(new MissionSuggestionPipelineOptions { UseMissionSuggestionPipeline = true }),
            NullLogger<RescueMissionSuggestionService>.Instance);

        var result = await service.PreviewSuggestionAsync(
            [new SosRequestSummary { Id = 1, RawMessage = "Need rescue" }],
            [],
            [],
            isMultiDepotRecommended: false,
            clusterId: 7,
            promptOverride: new PromptModel
            {
                Id = 12,
                Name = "Draft mission planning prompt",
                PromptType = PromptType.MissionPlanning,
                Provider = AiProvider.Gemini,
                Model = "gemini-preview",
                SystemPrompt = "Return mission JSON.",
                UserPromptTemplate = "{{sos_requests_data}}",
                IsActive = false
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.SuggestionId);
        Assert.Equal("Preview plan", result.SuggestedMissionTitle);
        Assert.Equal("legacy", result.PipelineMetadata?.ExecutionMode);
        Assert.Equal(0, suggestionRepository.CreateCalls);
        Assert.Equal(0, suggestionRepository.UpdateCalls);
        Assert.Equal(0, suggestionRepository.SavePipelineSnapshotCalls);
    }

    [Fact]
    public async Task PreviewSuggestionAsync_PipelinePrompt_OverridesMatchingStageOnly()
    {
        var suggestionRepository = new RecordingMissionAiSuggestionRepository();
        var promptRepository = new RecordingPromptRepository([
            BuildStagePrompt(4, PromptType.MissionRequirementsAssessment, "active-requirements"),
            BuildStagePrompt(5, PromptType.MissionDepotPlanning, "active-depot"),
            BuildStagePrompt(6, PromptType.MissionTeamPlanning, "active-team"),
            BuildStagePrompt(7, PromptType.MissionPlanValidation, "active-validation"),
            BuildStagePrompt(8, PromptType.MissionPlanning, "active-legacy")
        ]);
        var aiClient = new PipelineStubAiProviderClient();
        var service = new RescueMissionSuggestionService(
            new StubAiProviderClientFactory(aiClient),
            new StubSettingsResolver(),
            promptRepository,
            suggestionRepository,
            ThrowingProxy<IDepotInventoryRepository>.Create(),
            ThrowingProxy<IItemModelMetadataRepository>.Create(),
            new EmptyAssemblyPointRepository(),
            Options.Create(new MissionSuggestionPipelineOptions { UseMissionSuggestionPipeline = false }),
            NullLogger<RescueMissionSuggestionService>.Instance);

        var result = await service.PreviewSuggestionAsync(
            [new SosRequestSummary { Id = 1, RawMessage = "Need supplies" }],
            [],
            [],
            isMultiDepotRecommended: false,
            clusterId: 7,
            promptOverride: BuildStagePrompt(99, PromptType.MissionDepotPlanning, "override-depot", isActive: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.SuggestionId);
        Assert.Equal("Pipeline preview", result.SuggestedMissionTitle);
        var pipeline = result.PipelineMetadata;
        Assert.NotNull(pipeline);
        Assert.Equal("pipeline", pipeline!.ExecutionMode);
        Assert.Equal("override-depot", pipeline.Stages["depot"].ModelName);
        Assert.Contains("active-requirements", aiClient.Models);
        Assert.Contains("override-depot", aiClient.Models);
        Assert.Contains("active-team", aiClient.Models);
        Assert.Contains("active-validation", aiClient.Models);
        Assert.DoesNotContain("active-depot", aiClient.Models);
        Assert.Equal(0, suggestionRepository.CreateCalls);
        Assert.Equal(0, suggestionRepository.UpdateCalls);
        Assert.Equal(0, suggestionRepository.SavePipelineSnapshotCalls);
    }

    private static PromptModel BuildStagePrompt(
        int id,
        PromptType promptType,
        string model,
        bool isActive = true) => new()
        {
            Id = id,
            Name = $"{promptType} prompt",
            PromptType = promptType,
            Provider = AiProvider.Gemini,
            Model = model,
            SystemPrompt = $"System {promptType}",
            UserPromptTemplate = $"Template {promptType}",
            IsActive = isActive
        };

    private sealed class StubAiProviderClientFactory(IAiProviderClient client) : IAiProviderClientFactory
    {
        public IAiProviderClient GetClient(AiProvider provider) => client;
    }

    private sealed class StubAiProviderClient : IAiProviderClient
    {
        public AiProvider Provider => AiProvider.Gemini;

        public Task<AiCompletionResponse> CompleteAsync(
            AiCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiCompletionResponse
            {
                Text = """
                {
                  "mission_title": "Preview plan",
                  "mission_type": "MIXED",
                  "priority_score": 7.5,
                  "severity_level": "Moderate",
                  "overall_assessment": "Preview only",
                  "activities": [],
                  "resources": [],
                  "estimated_duration": "20 phut",
                  "special_notes": null,
                  "needs_additional_depot": false,
                  "supply_shortages": [],
                  "confidence_score": 0.9
                }
                """,
                HttpStatusCode = 200,
                LatencyMs = 10
            });
        }
    }

    private sealed class PipelineStubAiProviderClient : IAiProviderClient
    {
        public AiProvider Provider => AiProvider.Gemini;
        public List<string> Models { get; } = [];

        public Task<AiCompletionResponse> CompleteAsync(
            AiCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            Models.Add(request.Model);
            var text = request.Model switch
            {
                "active-requirements" => """
                {
                  "suggested_mission_title": "Pipeline preview draft",
                  "suggested_mission_type": "SUPPLY",
                  "suggested_priority_score": 6.5,
                  "suggested_severity_level": "Moderate",
                  "overall_assessment": "Assess request",
                  "estimated_duration": "20 phut",
                  "special_notes": null,
                  "needs_additional_depot": false,
                  "supply_shortages": [],
                  "confidence_score": 0.9,
                  "suggested_resources": [],
                  "sos_requirements": [
                    {
                      "sos_request_id": 1,
                      "summary": "Need supplies",
                      "priority": "High",
                      "required_supplies": [],
                      "required_teams": []
                    }
                  ]
                }
                """,
                "override-depot" => """
                {
                  "activities": [],
                  "special_notes": null,
                  "needs_additional_depot": false,
                  "supply_shortages": [],
                  "confidence_score": 0.8
                }
                """,
                "active-team" => """
                {
                  "activity_assignments": [],
                  "additional_activities": [],
                  "suggested_team": null,
                  "special_notes": null,
                  "confidence_score": 0.8
                }
                """,
                "active-validation" => """
                {
                  "mission_title": "Pipeline preview",
                  "mission_type": "SUPPLY",
                  "priority_score": 6.5,
                  "severity_level": "Moderate",
                  "overall_assessment": "Preview only",
                  "activities": [],
                  "resources": [],
                  "estimated_duration": "20 phut",
                  "special_notes": null,
                  "needs_additional_depot": false,
                  "supply_shortages": [],
                  "confidence_score": 0.9
                }
                """,
                _ => throw new InvalidOperationException($"Unexpected model {request.Model}.")
            };

            return Task.FromResult(new AiCompletionResponse
            {
                Text = text,
                HttpStatusCode = 200,
                LatencyMs = 10
            });
        }
    }

    private sealed class StubSettingsResolver : IAiPromptExecutionSettingsResolver
    {
        public AiPromptExecutionSettings Resolve(PromptModel prompt, AiPromptExecutionFallback fallback)
        {
            return new AiPromptExecutionSettings(
                prompt.Provider,
                prompt.Model ?? fallback.GeminiModel,
                "https://example.test/{0}/{1}",
                "test-key",
                prompt.Temperature ?? fallback.Temperature,
                prompt.MaxTokens ?? fallback.MaxTokens);
        }
    }

    private sealed class RecordingPromptRepository(IEnumerable<PromptModel> prompts) : IPromptRepository
    {
        private readonly List<PromptModel> _prompts = prompts.ToList();

        public Task<PromptModel?> GetActiveByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default)
            => Task.FromResult(_prompts.FirstOrDefault(prompt => prompt.PromptType == promptType && prompt.IsActive));

        public Task<PromptModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_prompts.FirstOrDefault(prompt => prompt.Id == id));

        public Task CreateAsync(PromptModel prompt, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(PromptModel prompt, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> ExistsVersionAsync(PromptType promptType, string version, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<PromptModel>> GetVersionsByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PromptModel>>(_prompts.Where(p => p.PromptType == promptType).ToList());

        public Task DeactivateOthersByTypeAsync(int currentPromptId, PromptType promptType, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<RESQ.Application.Common.Models.PagedResult<PromptModel>> GetAllPagedAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class EmptyAssemblyPointRepository : IAssemblyPointRepository
    {
        public Task CreateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<AssemblyPointModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<AssemblyPointModel?>(null);

        public Task<AssemblyPointModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult<AssemblyPointModel?>(null);

        public Task<AssemblyPointModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult<AssemblyPointModel?>(null);

        public Task<RESQ.Application.Common.Models.PagedResult<AssemblyPointModel>> GetAllPagedAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default,
            string? statusFilter = null)
            => throw new NotImplementedException();

        public Task UnassignAllRescuersAsync(int assemblyPointId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<List<AssemblyPointModel>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<List<AssemblyPointModel>>([]);

        public Task<Dictionary<int, List<RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById.AssemblyPointTeamDto>>> GetTeamsByAssemblyPointIdsAsync(
            IEnumerable<int> ids,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Dictionary<int, List<RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById.AssemblyPointTeamDto>>>([]);

        public Task<List<Guid>> GetAssignedRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default)
            => Task.FromResult<List<Guid>>([]);

        public Task<List<Guid>> GetTeamlessRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default)
            => Task.FromResult<List<Guid>>([]);

        public Task<bool> HasActiveTeamAsync(Guid rescuerUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task UpdateRescuerAssemblyPointAsync(Guid rescuerUserId, int? assemblyPointId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<List<Guid>> BulkUpdateRescuerAssemblyPointAsync(
            IReadOnlyList<Guid> userIds,
            int? assemblyPointId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<List<Guid>>([]);

        public Task<List<Guid>> FilterUsersWithoutActiveTeamAsync(
            IReadOnlyList<Guid> userIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<List<Guid>>([]);
    }

    private sealed class RecordingMissionAiSuggestionRepository : IMissionAiSuggestionRepository
    {
        public int CreateCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public int SavePipelineSnapshotCalls { get; private set; }

        public Task<int> CreateAsync(MissionAiSuggestionModel model, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(123);
        }

        public Task UpdateAsync(MissionAiSuggestionModel model, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }

        public Task SavePipelineSnapshotAsync(
            int suggestionId,
            MissionSuggestionMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            SavePipelineSnapshotCalls++;
            return Task.CompletedTask;
        }

        public Task<MissionAiSuggestionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<MissionAiSuggestionModel?>(null);

        public Task<IEnumerable<MissionAiSuggestionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<MissionAiSuggestionModel>>([]);

        public Task<IEnumerable<MissionAiSuggestionModel>> GetByClusterIdsAsync(IEnumerable<int> clusterIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<MissionAiSuggestionModel>>([]);
    }

    private class ThrowingProxy<T> : DispatchProxy
        where T : class
    {
        public static T Create() => DispatchProxy.Create<T, ThrowingProxy<T>>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            throw new NotImplementedException(targetMethod?.Name ?? typeof(T).Name);
        }
    }
}
