using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;
using RESQ.Infrastructure.Services;
using RESQ.Infrastructure.Services.Ai;

namespace RESQ.Tests.Infrastructure.Services;

public class RescueMissionSuggestionServicePreviewTests
{
    [Fact]
    public async Task PreviewSuggestionAsync_MissionPlanningPrompt_DoesNotPersistSuggestion()
    {
        var suggestionRepository = new RecordingMissionAiSuggestionRepository();
        var service = new RescueMissionSuggestionService(
            new StubAiProviderClientFactory(new LegacyStubAiProviderClient()),
            new AiPromptExecutionSettingsResolver(),
            new RecordingAiConfigRepository(BuildAiConfig()),
            ThrowingProxy<IPromptRepository>.Create(),
            suggestionRepository,
            ThrowingProxy<IDepotInventoryRepository>.Create(),
            ThrowingProxy<IItemModelMetadataRepository>.Create(),
            new EmptyAssemblyPointRepository(),
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
                SystemPrompt = "mission-planning-legacy",
                UserPromptTemplate = "{{sos_requests_data}}",
                IsActive = false
            },
            aiConfigOverride: BuildAiConfig(model: "gemini-preview"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.SuggestionId);
        Assert.Contains("MissionPlanning", result.ErrorMessage ?? string.Empty);
        Assert.Null(result.PipelineMetadata);
        Assert.Equal(0, suggestionRepository.CreateCalls);
        Assert.Equal(0, suggestionRepository.UpdateCalls);
        Assert.Equal(0, suggestionRepository.SavePipelineSnapshotCalls);
    }

    [Fact]
    public async Task PreviewSuggestionAsync_PipelinePrompt_OverridesMatchingStageOnly()
    {
        var suggestionRepository = new RecordingMissionAiSuggestionRepository();
        var promptRepository = new RecordingPromptRepository(
        [
            BuildStagePrompt(4, PromptType.MissionRequirementsAssessment, "active-requirements"),
            BuildStagePrompt(5, PromptType.MissionDepotPlanning, "active-depot"),
            BuildStagePrompt(6, PromptType.MissionTeamPlanning, "active-team"),
            BuildStagePrompt(7, PromptType.MissionPlanValidation, "active-validation"),
            BuildStagePrompt(8, PromptType.MissionPlanning, "active-legacy")
        ]);
        var aiClient = new PipelineStubAiProviderClient();
        var service = new RescueMissionSuggestionService(
            new StubAiProviderClientFactory(aiClient),
            new AiPromptExecutionSettingsResolver(),
            new RecordingAiConfigRepository(BuildAiConfig()),
            promptRepository,
            suggestionRepository,
            ThrowingProxy<IDepotInventoryRepository>.Create(),
            ThrowingProxy<IItemModelMetadataRepository>.Create(),
            new EmptyAssemblyPointRepository(),
            NullLogger<RescueMissionSuggestionService>.Instance);

        var result = await service.PreviewSuggestionAsync(
            [new SosRequestSummary { Id = 1, RawMessage = "Need supplies" }],
            [],
            [],
            isMultiDepotRecommended: false,
            clusterId: 7,
            promptOverride: BuildStagePrompt(99, PromptType.MissionDepotPlanning, "override-depot", isActive: false),
            aiConfigOverride: BuildAiConfig(model: "shared-preview-model"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.SuggestionId);
        Assert.Equal("Pipeline preview", result.SuggestedMissionTitle);
        var pipeline = result.PipelineMetadata;
        Assert.NotNull(pipeline);
        Assert.Equal("pipeline", pipeline!.ExecutionMode);
        Assert.Equal("shared-preview-model", pipeline.Stages["depot"].ModelName);
        Assert.Contains("active-requirements", aiClient.StageMarkers);
        Assert.Contains("override-depot", aiClient.StageMarkers);
        Assert.Contains("active-team", aiClient.StageMarkers);
        Assert.Contains("active-validation", aiClient.StageMarkers);
        Assert.DoesNotContain("active-depot", aiClient.StageMarkers);
        Assert.Equal(0, suggestionRepository.CreateCalls);
        Assert.Equal(0, suggestionRepository.UpdateCalls);
        Assert.Equal(0, suggestionRepository.SavePipelineSnapshotCalls);
    }

    [Fact]
    public async Task PreviewSuggestionAsync_LegacyPrompt_MapsBoatResourceIntoCollectAndReturnActivities()
    {
        var suggestionRepository = new RecordingMissionAiSuggestionRepository();
        var service = new RescueMissionSuggestionService(
            new StubAiProviderClientFactory(new LegacyTransportStubAiProviderClient()),
            new AiPromptExecutionSettingsResolver(),
            new RecordingAiConfigRepository(BuildAiConfig()),
            ThrowingProxy<IPromptRepository>.Create(),
            suggestionRepository,
            CreateInventoryBackedTransportDepotRepository(),
            CreateInventoryBackedTransportItemMetadataRepository(),
            new EmptyAssemblyPointRepository(),
            NullLogger<RescueMissionSuggestionService>.Instance);

        var result = await service.PreviewSuggestionAsync(
            [
                new SosRequestSummary
                {
                    Id = 1,
                    RawMessage = "Khu vuc ngap sau, can ca no cuu ho de so tan nguoi mac ket.",
                    PriorityLevel = "Critical",
                    Latitude = 16.4661,
                    Longitude = 107.5978
                }
            ],
            [
                new DepotSummary
                {
                    Id = 1,
                    Name = "Kho Hue",
                    Address = "1 Le Loi",
                    Latitude = 16.4545,
                    Longitude = 107.5680,
                    Inventories =
                    [
                        new DepotInventoryItemDto { ItemId = 15, ItemName = "Nuoc sach", Unit = "chai", AvailableQuantity = 30 },
                        new DepotInventoryItemDto { ItemId = 105, ItemName = "Ca no cuu ho", Unit = "chiec", AvailableQuantity = 4 }
                    ]
                }
            ],
            [],
            isMultiDepotRecommended: false,
            clusterId: 7,
            promptOverride: new PromptModel
            {
                Id = 12,
                Name = "Draft mission planning prompt",
                PromptType = PromptType.MissionPlanning,
                SystemPrompt = "mission-planning-legacy",
                UserPromptTemplate = "{{sos_requests_data}}",
                IsActive = false
            },
            aiConfigOverride: BuildAiConfig(model: "gemini-preview"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("MissionPlanning", result.ErrorMessage ?? string.Empty);
        Assert.Empty(result.SuggestedActivities);
        Assert.Empty(result.SuggestedResources);
    }

    [Fact]
    public async Task GenerateSuggestionAsync_DepotStageFailure_DoesNotFallbackToLegacyPrompt_AndPersistsFailureSnapshot()
    {
        var suggestionRepository = new RecordingMissionAiSuggestionRepository();
        var aiClient = new PipelineStubAiProviderClient(failingStage: "active-depot");
        var service = new RescueMissionSuggestionService(
            new StubAiProviderClientFactory(aiClient),
            new AiPromptExecutionSettingsResolver(),
            new RecordingAiConfigRepository(BuildAiConfig()),
            CreatePipelinePromptRepository(),
            suggestionRepository,
            ThrowingProxy<IDepotInventoryRepository>.Create(),
            ThrowingProxy<IItemModelMetadataRepository>.Create(),
            new EmptyAssemblyPointRepository(),
            NullLogger<RescueMissionSuggestionService>.Instance);

        var result = await service.GenerateSuggestionAsync(
            [new SosRequestSummary { Id = 1, RawMessage = "Need rescue supplies" }],
            [],
            [],
            isMultiDepotRecommended: false,
            clusterId: 7,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(123, result.SuggestionId);
        Assert.Contains("active-depot failed", result.ErrorMessage ?? string.Empty);
        Assert.NotNull(result.PipelineMetadata);
        Assert.Equal("failed", result.PipelineMetadata!.PipelineStatus);
        Assert.Equal("failed", result.PipelineMetadata.FinalResultSource);
        Assert.Equal("depot", result.PipelineMetadata.FailedStage);
        Assert.Contains("active-depot failed", result.PipelineMetadata.FailureReason ?? string.Empty);
        Assert.Equal(["active-requirements", "active-depot"], aiClient.StageMarkers);
        Assert.Equal(1, suggestionRepository.CreateCalls);
        Assert.Equal(0, suggestionRepository.UpdateCalls);
        Assert.True(suggestionRepository.SavePipelineSnapshotCalls > 0);
        Assert.NotNull(suggestionRepository.LastSavedMetadata);
        Assert.False(suggestionRepository.LastSavedMetadata!.IsSuccess);
        Assert.Equal("failed", suggestionRepository.LastSavedMetadata.Pipeline!.PipelineStatus);
        Assert.Equal("depot", suggestionRepository.LastSavedMetadata.Pipeline.FailedStage);
    }

    [Fact]
    public async Task PreviewSuggestionAsync_ValidationStageFailure_ReturnsFailedResult_InsteadOfDraftSuggestion()
    {
        var suggestionRepository = new RecordingMissionAiSuggestionRepository();
        var aiClient = new PipelineStubAiProviderClient();
        var service = new RescueMissionSuggestionService(
            new StubAiProviderClientFactory(aiClient),
            new AiPromptExecutionSettingsResolver(),
            new RecordingAiConfigRepository(BuildAiConfig()),
            new RecordingPromptRepository(
            [
                BuildStagePrompt(4, PromptType.MissionRequirementsAssessment, "active-requirements"),
                BuildStagePrompt(5, PromptType.MissionDepotPlanning, "active-depot"),
                BuildStagePrompt(6, PromptType.MissionTeamPlanning, "active-team"),
                BuildStagePrompt(8, PromptType.MissionPlanning, "legacy-should-not-run")
            ]),
            suggestionRepository,
            ThrowingProxy<IDepotInventoryRepository>.Create(),
            ThrowingProxy<IItemModelMetadataRepository>.Create(),
            new EmptyAssemblyPointRepository(),
            NullLogger<RescueMissionSuggestionService>.Instance);

        var result = await service.PreviewSuggestionAsync(
            [new SosRequestSummary { Id = 1, RawMessage = "Need rescue supplies" }],
            [],
            [],
            isMultiDepotRecommended: false,
            clusterId: 7,
            promptOverride: BuildStagePrompt(99, PromptType.MissionDepotPlanning, "override-depot", isActive: false),
            aiConfigOverride: BuildAiConfig(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.SuggestionId);
        Assert.Empty(result.SuggestedActivities);
        Assert.Empty(result.SuggestedResources);
        Assert.NotNull(result.PipelineMetadata);
        Assert.Equal("failed", result.PipelineMetadata!.PipelineStatus);
        Assert.Equal("failed", result.PipelineMetadata.FinalResultSource);
        Assert.Equal("validate", result.PipelineMetadata.FailedStage);
        Assert.DoesNotContain("draft", result.PipelineMetadata.FinalResultSource ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            ["active-requirements", "override-depot", "active-team"],
            aiClient.StageMarkers);
        Assert.Equal(0, suggestionRepository.CreateCalls);
        Assert.Equal(0, suggestionRepository.UpdateCalls);
        Assert.Equal(0, suggestionRepository.SavePipelineSnapshotCalls);
    }

    [Fact]
    public async Task GenerateSuggestionStreamAsync_PipelineFailure_EmitsStructuredErrorResult()
    {
        var suggestionRepository = new RecordingMissionAiSuggestionRepository();
        var aiClient = new PipelineStubAiProviderClient(failingStage: "active-depot");
        var service = new RescueMissionSuggestionService(
            new StubAiProviderClientFactory(aiClient),
            new AiPromptExecutionSettingsResolver(),
            new RecordingAiConfigRepository(BuildAiConfig()),
            CreatePipelinePromptRepository(),
            suggestionRepository,
            ThrowingProxy<IDepotInventoryRepository>.Create(),
            ThrowingProxy<IItemModelMetadataRepository>.Create(),
            new EmptyAssemblyPointRepository(),
            NullLogger<RescueMissionSuggestionService>.Instance);

        var events = new List<SseMissionEvent>();
        await foreach (var evt in service.GenerateSuggestionStreamAsync(
            [new SosRequestSummary { Id = 1, RawMessage = "Need rescue supplies" }],
            [],
            [],
            isMultiDepotRecommended: false,
            clusterId: 7,
            CancellationToken.None))
        {
            events.Add(evt);
        }

        var errorEvent = Assert.Single(events, evt => evt.EventType == "error");
        Assert.Null(events.SingleOrDefault(evt => evt.EventType == "result"));
        Assert.NotNull(errorEvent.Result);
        Assert.False(errorEvent.Result!.IsSuccess);
        Assert.Equal(123, errorEvent.Result.SuggestionId);
        Assert.Equal("depot", errorEvent.Result.PipelineMetadata?.FailedStage);
        Assert.Equal("failed", errorEvent.Result.PipelineMetadata?.PipelineStatus);
        Assert.Contains("active-depot failed", errorEvent.Data ?? string.Empty);
    }

    private static PromptModel BuildStagePrompt(
        int id,
        PromptType promptType,
        string marker,
        bool isActive = true) => new()
        {
            Id = id,
            Name = $"{promptType} prompt",
            PromptType = promptType,
            Purpose = $"Purpose {promptType}",
            SystemPrompt = $"stage-marker:{marker}",
            UserPromptTemplate = $"template-marker:{marker}",
            Version = "v1.0",
            IsActive = isActive
        };

    private static RecordingPromptRepository CreatePipelinePromptRepository()
        => new(
        [
            BuildStagePrompt(4, PromptType.MissionRequirementsAssessment, "active-requirements"),
            BuildStagePrompt(5, PromptType.MissionDepotPlanning, "active-depot"),
            BuildStagePrompt(6, PromptType.MissionTeamPlanning, "active-team"),
            BuildStagePrompt(7, PromptType.MissionPlanValidation, "active-validation"),
            BuildStagePrompt(8, PromptType.MissionPlanning, "legacy-should-not-run")
        ]);

    private static AiConfigModel BuildAiConfig(string model = "gemini-2.5-flash") => new()
    {
        Id = 7,
        Name = "Preview AI Config",
        Provider = AiProvider.Gemini,
        Model = model,
        ApiUrl = "https://example.test/{0}/{1}",
        ApiKey = "test-key",
        Temperature = 0.2,
        MaxTokens = 4096,
        Version = "v1.0",
        IsActive = true
    };

    private static IDepotInventoryRepository CreateInventoryBackedTransportDepotRepository()
    {
        return ThrowingProxy<IDepotInventoryRepository>.Create((method, _) =>
            method?.Name switch
            {
                nameof(IDepotInventoryRepository.SearchForAgentAsync) => Task.FromResult((
                    new List<AgentInventoryItem>
                    {
                        new()
                        {
                            ItemId = 105,
                            ItemName = "Ca no cuu ho",
                            CategoryName = "Phuong tien",
                            ItemType = "Reusable",
                            Unit = "chiec",
                            AvailableQuantity = 4,
                            GoodAvailableCount = 4,
                            DepotId = 1,
                            DepotName = "Kho Hue",
                            DepotAddress = "1 Le Loi",
                            DepotLatitude = 16.4545,
                            DepotLongitude = 107.5680
                        }
                    },
                    1)),
                nameof(IDepotInventoryRepository.PreviewReserveSuppliesAsync) => Task.FromResult(
                    new MissionSupplyReservationResult
                    {
                        Items =
                        [
                            new SupplyExecutionItemDto
                            {
                                ItemModelId = 105,
                                ItemName = "Ca no cuu ho",
                                Unit = "chiec",
                                Quantity = 2,
                                ReusableUnits =
                                [
                                    new SupplyExecutionReusableUnitDto { ReusableItemId = 501, ItemModelId = 105, ItemName = "Ca no cuu ho", SerialNumber = "CN-001", Condition = "Good" },
                                    new SupplyExecutionReusableUnitDto { ReusableItemId = 502, ItemModelId = 105, ItemName = "Ca no cuu ho", SerialNumber = "CN-002", Condition = "Good" }
                                ]
                            }
                        ]
                    }),
                nameof(IDepotInventoryRepository.GetDepotLocationAsync) => Task.FromResult<(double Latitude, double Longitude)?>(null),
                _ => throw new NotImplementedException(method?.Name ?? typeof(IDepotInventoryRepository).Name)
            });
    }

    private static IItemModelMetadataRepository CreateInventoryBackedTransportItemMetadataRepository()
    {
        return ThrowingProxy<IItemModelMetadataRepository>.Create((method, args) =>
            method?.Name switch
            {
                nameof(IItemModelMetadataRepository.GetByIdsAsync) => Task.FromResult(
                    (((IReadOnlyList<int>)args![0]!).Contains(105))
                        ? new Dictionary<int, ItemModelRecord>
                        {
                            [105] = new()
                            {
                                Id = 105,
                                CategoryId = 10,
                                Name = "Ca no cuu ho",
                                Unit = "chiec",
                                ItemType = "Reusable"
                            }
                        }
                        : new Dictionary<int, ItemModelRecord>()),
                _ => throw new NotImplementedException(method?.Name ?? typeof(IItemModelMetadataRepository).Name)
            });
    }

    private sealed class StubAiProviderClientFactory(IAiProviderClient client) : IAiProviderClientFactory
    {
        public IAiProviderClient GetClient(AiProvider provider) => client;
    }

    private sealed class LegacyStubAiProviderClient : IAiProviderClient
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

    private sealed class LegacyTransportStubAiProviderClient : IAiProviderClient
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
                  "mission_title": "Boat preview",
                  "mission_type": "MIXED",
                  "priority_score": 8.8,
                  "severity_level": "Critical",
                  "overall_assessment": "Flooded area requires rescue and supply support",
                  "activities": [
                    {
                      "step": 1,
                      "activity_type": "COLLECT_SUPPLIES",
                      "description": "Lay nuoc tu Kho Hue",
                      "sos_request_id": 1,
                      "depot_id": 1,
                      "depot_name": "Kho Hue",
                      "depot_address": "1 Le Loi",
                      "supplies_to_collect": [
                        { "item_id": 15, "item_name": "Nuoc sach", "quantity": 10, "unit": "chai" }
                      ],
                      "priority": "Critical",
                      "estimated_time": "30 phut",
                      "suggested_team": null
                    },
                    {
                      "step": 2,
                      "activity_type": "RESCUE",
                      "description": "Cuu ho nguoi mac ket tai SOS ID 1",
                      "sos_request_id": 1,
                      "depot_id": null,
                      "depot_name": null,
                      "depot_address": null,
                      "supplies_to_collect": null,
                      "priority": "Critical",
                      "estimated_time": "1 gio",
                      "suggested_team": {
                        "team_id": 21,
                        "team_name": "Team A",
                        "team_type": "Rescue",
                        "reason": "Gan nhat",
                        "assembly_point_id": 1,
                        "assembly_point_name": "AP 1",
                        "latitude": 16.46,
                        "longitude": 107.58
                      }
                    }
                  ],
                  "resources": [
                    { "resource_type": "BOAT", "description": "Ca no cuu ho cho khu vuc ngap sau", "quantity": 2, "priority": "Critical" }
                  ],
                  "estimated_duration": "1 gio 30 phut",
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
        private readonly string? _failingStage;

        public PipelineStubAiProviderClient(string? failingStage = null)
        {
            _failingStage = failingStage;
        }

        public AiProvider Provider => AiProvider.Gemini;
        public List<string> StageMarkers { get; } = [];

        public Task<AiCompletionResponse> CompleteAsync(
            AiCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            var marker = ExtractMarker(request.SystemPrompt);
            StageMarkers.Add(marker);

            if (string.Equals(marker, _failingStage, StringComparison.Ordinal))
                throw new InvalidOperationException($"{marker} failed");

            var text = marker switch
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
                _ => throw new InvalidOperationException($"Unexpected stage marker {marker}.")
            };

            return Task.FromResult(new AiCompletionResponse
            {
                Text = text,
                HttpStatusCode = 200,
                LatencyMs = 10
            });
        }

        private static string ExtractMarker(string? systemPrompt)
        {
            const string prefix = "stage-marker:";
            if (string.IsNullOrWhiteSpace(systemPrompt))
                return string.Empty;

            var start = systemPrompt.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0)
                return string.Empty;

            start += prefix.Length;
            var end = systemPrompt.IndexOfAny(['\r', '\n'], start);
            return end >= 0
                ? systemPrompt[start..end].Trim()
                : systemPrompt[start..].Trim();
        }
    }

    private sealed class RecordingAiConfigRepository(AiConfigModel activeConfig) : IAiConfigRepository
    {
        public Task<AiConfigModel?> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<AiConfigModel?>(activeConfig);

        public Task<AiConfigModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(activeConfig.Id == id ? activeConfig : null);

        public Task CreateAsync(AiConfigModel config, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(AiConfigModel config, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsVersionAsync(string version, int? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<AiConfigModel>> GetVersionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AiConfigModel>>([activeConfig]);
        public Task DeactivateOthersAsync(int currentConfigId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<RESQ.Application.Common.Models.PagedResult<AiConfigModel>> GetAllPagedAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();
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
        public Task<bool> ExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsVersionAsync(PromptType promptType, string version, int? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<PromptModel>> GetVersionsByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PromptModel>>(_prompts.Where(p => p.PromptType == promptType).ToList());
        public Task DeactivateOthersByTypeAsync(int currentPromptId, PromptType promptType, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<RESQ.Application.Common.Models.PagedResult<PromptModel>> GetAllPagedAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class EmptyAssemblyPointRepository : IAssemblyPointRepository
    {
        public Task CreateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AssemblyPointModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<AssemblyPointModel?>(null);
        public Task<AssemblyPointModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<AssemblyPointModel?>(null);
        public Task<AssemblyPointModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult<AssemblyPointModel?>(null);
        public Task<RESQ.Application.Common.Models.PagedResult<AssemblyPointModel>> GetAllPagedAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default,
            string? statusFilter = null) => throw new NotImplementedException();
        public Task UnassignAllRescuersAsync(int assemblyPointId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<AssemblyPointModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<List<AssemblyPointModel>>([]);
        public Task<Dictionary<int, List<RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById.AssemblyPointTeamDto>>> GetTeamsByAssemblyPointIdsAsync(
            IEnumerable<int> ids,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Dictionary<int, List<RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById.AssemblyPointTeamDto>>>([]);
        public Task<List<Guid>> GetAssignedRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default) => Task.FromResult<List<Guid>>([]);
        public Task<List<Guid>> GetTeamlessRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default) => Task.FromResult<List<Guid>>([]);
        public Task<bool> HasActiveTeamAsync(Guid rescuerUserId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task UpdateRescuerAssemblyPointAsync(Guid rescuerUserId, int? assemblyPointId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<Guid>> BulkUpdateRescuerAssemblyPointAsync(IReadOnlyList<Guid> userIds, int? assemblyPointId, CancellationToken cancellationToken = default) => Task.FromResult<List<Guid>>([]);
        public Task<List<Guid>> FilterUsersWithoutActiveTeamAsync(IReadOnlyList<Guid> userIds, CancellationToken cancellationToken = default) => Task.FromResult<List<Guid>>([]);
    }

    private sealed class RecordingMissionAiSuggestionRepository : IMissionAiSuggestionRepository
    {
        public int CreateCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public int SavePipelineSnapshotCalls { get; private set; }
        public MissionAiSuggestionModel? LastCreatedModel { get; private set; }
        public MissionSuggestionMetadata? LastSavedMetadata { get; private set; }

        public Task<int> CreateAsync(MissionAiSuggestionModel model, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            LastCreatedModel = model;
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
            LastSavedMetadata = metadata;
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
        private Func<MethodInfo?, object?[]?, object?>? _handler;

        public static T Create() => DispatchProxy.Create<T, ThrowingProxy<T>>();

        public static T Create(Func<MethodInfo?, object?[]?, object?> handler)
        {
            var proxy = DispatchProxy.Create<T, ThrowingProxy<T>>();
            ((ThrowingProxy<T>)(object)proxy)._handler = handler;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (_handler is not null)
                return _handler(targetMethod, args);

            throw new NotImplementedException(targetMethod?.Name ?? typeof(T).Name);
        }
    }
}
