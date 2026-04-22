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
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;
using RESQ.Infrastructure.Options;
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
                SystemPrompt = "mission-planning-legacy",
                UserPromptTemplate = "{{sos_requests_data}}",
                IsActive = false
            },
            aiConfigOverride: BuildAiConfig(model: "gemini-preview"),
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
    public async Task PreviewSuggestionAsync_MissionPlanningPrompt_MixedClusterWithoutActivities_BuildsBestEffortFallbackRoute()
    {
        var service = new RescueMissionSuggestionService(
            new StubAiProviderClientFactory(new LegacyStubAiProviderClient(
                """
                {
                  "mission_title": "Preview plan",
                  "mission_type": "MIXED",
                  "priority_score": 7.5,
                  "severity_level": "Critical",
                  "overall_assessment": "Preview only",
                  "activities": [],
                  "resources": [],
                  "estimated_duration": "20 phut",
                  "special_notes": "Nen tach cluster neu can.",
                  "needs_additional_depot": false,
                  "supply_shortages": [],
                  "confidence_score": 0.9
                }
                """)),
            new AiPromptExecutionSettingsResolver(),
            new RecordingAiConfigRepository(BuildAiConfig()),
            ThrowingProxy<IPromptRepository>.Create(),
            new RecordingMissionAiSuggestionRepository(),
            ThrowingProxy<IDepotInventoryRepository>.Create(),
            ThrowingProxy<IItemModelMetadataRepository>.Create(),
            new EmptyAssemblyPointRepository(),
            Options.Create(new MissionSuggestionPipelineOptions { UseMissionSuggestionPipeline = true }),
            NullLogger<RescueMissionSuggestionService>.Instance);

        var result = await service.PreviewSuggestionAsync(
            [
                new SosRequestSummary
                {
                    Id = 11,
                    SosType = "Rescue",
                    RawMessage = "Nan nhan can dua den noi an toan ngay",
                    PriorityLevel = "Critical",
                    AiAnalysis = new SosRequestAiAnalysisSummary
                    {
                        HasAiAnalysis = true,
                        SuggestedPriority = "Critical",
                        NeedsImmediateSafeTransfer = true,
                        CanWaitForCombinedMission = false
                    }
                },
                new SosRequestSummary
                {
                    Id = 22,
                    SosType = "Relief",
                    RawMessage = "Can tiep te luong thuc va nuoc uong",
                    PriorityLevel = "High",
                    AiAnalysis = new SosRequestAiAnalysisSummary
                    {
                        HasAiAnalysis = true,
                        SuggestedPriority = "High",
                        NeedsImmediateSafeTransfer = false,
                        CanWaitForCombinedMission = true
                    }
                }
            ],
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

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        Assert.True(result.NeedsManualReview);
        Assert.NotEmpty(result.MixedRescueReliefWarning);
        Assert.Contains(
            result.SuggestedActivities,
            activity => string.Equals(activity.ActivityType, "RESCUE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            result.SuggestedActivities,
            activity => string.Equals(activity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase));
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
            Options.Create(new MissionSuggestionPipelineOptions { UseMissionSuggestionPipeline = false }),
            NullLogger<RescueMissionSuggestionService>.Instance);

        var result = await service.PreviewSuggestionAsync(
            [new SosRequestSummary { Id = 1, RawMessage = "Need supplies" }],
            [
                new DepotSummary
                {
                    Id = 9,
                    Name = "Kho Preview",
                    Address = "1 Preview Street",
                    Latitude = 16.463,
                    Longitude = 107.590
                }
            ],
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
    public async Task PreviewSuggestionAsync_PipelineValidationWithoutActivities_FailsHard()
    {
        var promptRepository = new RecordingPromptRepository(
        [
            BuildStagePrompt(4, PromptType.MissionRequirementsAssessment, "requirements-fallback"),
            BuildStagePrompt(5, PromptType.MissionDepotPlanning, "depot-fallback"),
            BuildStagePrompt(6, PromptType.MissionTeamPlanning, "team-fallback"),
            BuildStagePrompt(7, PromptType.MissionPlanValidation, "validation-empty"),
            BuildStagePrompt(8, PromptType.MissionPlanning, "active-legacy")
        ]);
        var aiClient = new PipelineStubAiProviderClient(new Dictionary<string, string>
        {
            ["requirements-fallback"] = """
            {
              "suggested_mission_title": "Pipeline mixed mission",
              "suggested_mission_type": "MIXED",
              "suggested_priority_score": 9.4,
              "suggested_severity_level": "Critical",
              "overall_assessment": "Mixed urgent mission",
              "estimated_duration": "1 gio 20 phut",
              "special_notes": "Canh bao tach cluster.",
              "split_cluster_recommended": true,
              "split_cluster_reason": "Rescue khan cap dang bi ghep chung voi nhanh cuu tro.",
              "needs_additional_depot": false,
              "supply_shortages": [],
              "confidence_score": 0.91,
              "suggested_resources": [],
              "sos_requirements": [
                {
                  "sos_request_id": 11,
                  "summary": "Can cuu ho gap",
                  "priority": "Critical",
                  "needs_immediate_safe_transfer": true,
                  "can_wait_for_combined_mission": false,
                  "handling_reason": "Nan nhan can di chuyen den noi an toan ngay.",
                  "required_supplies": [],
                  "required_teams": []
                },
                {
                  "sos_request_id": 22,
                  "summary": "Can tiep te",
                  "priority": "High",
                  "needs_immediate_safe_transfer": false,
                  "can_wait_for_combined_mission": true,
                  "handling_reason": "Nhanh cuu tro co the di kem route an toan.",
                  "required_supplies": [],
                  "required_teams": []
                }
              ]
            }
            """,
            ["depot-fallback"] = """
            {
              "activities": [
                {
                  "activity_key": "collect-22",
                  "step": 1,
                  "activity_type": "COLLECT_SUPPLIES",
                  "description": "Lay hang tai kho Hue",
                  "priority": "High",
                  "estimated_time": "20 phut",
                  "sos_request_id": 22,
                  "depot_id": 9,
                  "depot_name": "Kho Hue",
                  "depot_address": "1 Tran Hung Dao",
                  "depot_latitude": 16.463,
                  "depot_longitude": 107.590,
                  "supplies_to_collect": [
                    {
                      "item_id": 501,
                      "item_name": "Sua dinh duong tre em",
                      "quantity": 4,
                      "unit": "hop"
                    }
                  ]
                },
                {
                  "activity_key": "deliver-22",
                  "step": 2,
                  "activity_type": "DELIVER_SUPPLIES",
                  "description": "Giao do cho SOS 22",
                  "priority": "High",
                  "estimated_time": "20 phut",
                  "sos_request_id": 22,
                  "depot_id": 9,
                  "depot_name": "Kho Hue",
                  "depot_address": "1 Tran Hung Dao",
                  "supplies_to_collect": [
                    {
                      "item_id": 501,
                      "item_name": "Sua dinh duong tre em",
                      "quantity": 4,
                      "unit": "hop"
                    }
                  ]
                }
              ],
              "special_notes": null,
              "needs_additional_depot": false,
              "supply_shortages": [],
              "confidence_score": 0.87
            }
            """,
            ["team-fallback"] = """
            {
              "activity_assignments": [
                {
                  "activity_key": "collect-22",
                  "execution_mode": "SingleTeam",
                  "required_team_count": 1,
                  "suggested_team": {
                    "team_id": 21,
                    "team_name": "Team 21",
                    "team_type": "Rescue",
                    "assembly_point_id": 7,
                    "assembly_point_name": "AP Hue",
                    "latitude": 16.470,
                    "longitude": 107.600,
                    "distance_km": 1.2
                  }
                },
                {
                  "activity_key": "deliver-22",
                  "execution_mode": "SingleTeam",
                  "required_team_count": 1,
                  "suggested_team": {
                    "team_id": 21,
                    "team_name": "Team 21",
                    "team_type": "Rescue",
                    "assembly_point_id": 7,
                    "assembly_point_name": "AP Hue",
                    "latitude": 16.470,
                    "longitude": 107.600,
                    "distance_km": 1.2
                  }
                }
              ],
              "additional_activities": [
                {
                  "activity_key": "rescue-11",
                  "step": 3,
                  "activity_type": "RESCUE",
                  "description": "Cuu ho SOS 11 va dua den diem an toan",
                  "priority": "Critical",
                  "estimated_time": "30 phut",
                  "sos_request_id": 11,
                  "assembly_point_id": 7,
                  "assembly_point_name": "AP Hue",
                  "assembly_point_latitude": 16.470,
                  "assembly_point_longitude": 107.600,
                  "suggested_team": {
                    "team_id": 21,
                    "team_name": "Team 21",
                    "team_type": "Rescue",
                    "assembly_point_id": 7,
                    "assembly_point_name": "AP Hue",
                    "latitude": 16.470,
                    "longitude": 107.600,
                    "distance_km": 1.2
                  }
                },
                {
                  "activity_key": "evacuate-11",
                  "step": 4,
                  "activity_type": "EVACUATE",
                  "description": "Dua SOS 11 den diem tap ket",
                  "priority": "Critical",
                  "estimated_time": "20 phut",
                  "sos_request_id": 11,
                  "assembly_point_id": 7,
                  "assembly_point_name": "AP Hue",
                  "assembly_point_latitude": 16.470,
                  "assembly_point_longitude": 107.600,
                  "suggested_team": {
                    "team_id": 21,
                    "team_name": "Team 21",
                    "team_type": "Rescue",
                    "assembly_point_id": 7,
                    "assembly_point_name": "AP Hue",
                    "latitude": 16.470,
                    "longitude": 107.600,
                    "distance_km": 1.2
                  }
                }
              ],
              "ordered_activity_keys": ["rescue-11", "evacuate-11", "collect-22", "deliver-22"],
              "suggested_team": {
                "team_id": 21,
                "team_name": "Team 21",
                "team_type": "Rescue",
                "assembly_point_id": 7,
                "assembly_point_name": "AP Hue",
                "latitude": 16.470,
                "longitude": 107.600,
                "distance_km": 1.2
              },
              "special_notes": "Canh bao tach cluster.",
              "confidence_score": 0.86
            }
            """,
            ["validation-empty"] = """
            {
              "mission_title": "Pipeline mixed mission",
              "mission_type": "MIXED",
              "priority_score": 9.4,
              "severity_level": "Critical",
              "overall_assessment": "Mixed urgent mission",
              "activities": [],
              "resources": [],
              "estimated_duration": "1 gio 20 phut",
              "special_notes": "Nen tach cluster thanh rescue rieng va relief rieng.",
              "needs_additional_depot": false,
              "supply_shortages": [],
              "confidence_score": 0.91
            }
            """
        });
        var service = new RescueMissionSuggestionService(
            new StubAiProviderClientFactory(aiClient),
            new AiPromptExecutionSettingsResolver(),
            new RecordingAiConfigRepository(BuildAiConfig()),
            promptRepository,
            new RecordingMissionAiSuggestionRepository(),
            ThrowingProxy<IDepotInventoryRepository>.Create(),
            new StubItemModelMetadataRepository(
                new ItemModelRecord
                {
                    Id = 501,
                    CategoryId = 1,
                    Name = "Sua dinh duong tre em",
                    Unit = "hop",
                    ItemType = "Consumable"
                }),
            new EmptyAssemblyPointRepository(),
            Options.Create(new MissionSuggestionPipelineOptions { UseMissionSuggestionPipeline = true }),
            NullLogger<RescueMissionSuggestionService>.Instance);

        var result = await service.PreviewSuggestionAsync(
            [
                new SosRequestSummary
                {
                    Id = 11,
                    SosType = "Rescue",
                    RawMessage = "Nan nhan can dua den noi an toan ngay",
                    AiAnalysis = new SosRequestAiAnalysisSummary
                    {
                        HasAiAnalysis = true,
                        SuggestedPriority = "Critical",
                        NeedsImmediateSafeTransfer = true,
                        CanWaitForCombinedMission = false
                    }
                },
                new SosRequestSummary
                {
                    Id = 22,
                    SosType = "Relief",
                    RawMessage = "Can tiep te luong thuc",
                    AiAnalysis = new SosRequestAiAnalysisSummary
                    {
                        HasAiAnalysis = true,
                        SuggestedPriority = "High",
                        NeedsImmediateSafeTransfer = false,
                        CanWaitForCombinedMission = true
                    }
                }
            ],
            [
                new DepotSummary
                {
                    Id = 9,
                    Name = "Kho Hue",
                    Address = "1 Tran Hung Dao",
                    Latitude = 16.463,
                    Longitude = 107.590,
                    Inventories =
                    [
                        new DepotInventoryItemDto
                        {
                            ItemId = 501,
                            ItemName = "Sua dinh duong tre em",
                            Unit = "hop",
                            AvailableQuantity = 20
                        }
                    ]
                }
            ],
            [
                new AgentTeamInfo
                {
                    TeamId = 21,
                    TeamName = "Team 21",
                    TeamType = "Rescue",
                    IsAvailable = true,
                    Status = "Available",
                    AssemblyPointId = 7,
                    AssemblyPointName = "AP Hue",
                    Latitude = 16.470,
                    Longitude = 107.600,
                    DistanceKm = 1.2
                }
            ],
            isMultiDepotRecommended: false,
            clusterId: 7,
            promptOverride: BuildStagePrompt(99, PromptType.MissionPlanValidation, "validation-empty", isActive: false),
            aiConfigOverride: BuildAiConfig(model: "shared-preview-model"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Validation stage failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.NeedsManualReview);
        Assert.Empty(result.SuggestedActivities);
        Assert.NotNull(result.PipelineMetadata);
        Assert.Equal("failed", result.PipelineMetadata!.PipelineStatus);
        Assert.Null(result.PipelineMetadata.FinalResultSource);
        Assert.False(result.PipelineMetadata.UsedLegacyFallback);
    }

    [Fact]
    public async Task PreviewSuggestionAsync_PipelineValidationReliefOnlyExecutable_FailsHard()
    {
        var promptRepository = new RecordingPromptRepository(
        [
            BuildStagePrompt(4, PromptType.MissionRequirementsAssessment, "requirements-prefer-draft"),
            BuildStagePrompt(5, PromptType.MissionDepotPlanning, "depot-prefer-draft"),
            BuildStagePrompt(6, PromptType.MissionTeamPlanning, "team-prefer-draft"),
            BuildStagePrompt(7, PromptType.MissionPlanValidation, "validation-relief-only"),
            BuildStagePrompt(8, PromptType.MissionPlanning, "active-legacy")
        ]);
        var aiClient = new PipelineStubAiProviderClient(new Dictionary<string, string>
        {
            ["requirements-prefer-draft"] = """
            {
              "suggested_mission_title": "Pipeline mixed mission",
              "suggested_mission_type": "MIXED",
              "suggested_priority_score": 9.1,
              "suggested_severity_level": "Critical",
              "overall_assessment": "Pipeline assembled a mixed route",
              "estimated_duration": "1 gio 10 phut",
              "special_notes": null,
              "needs_additional_depot": false,
              "supply_shortages": [],
              "confidence_score": 0.9,
              "suggested_resources": [],
              "sos_requirements": [
                {
                  "sos_request_id": 11,
                  "summary": "Can cuu ho khan cap",
                  "priority": "Critical",
                  "needs_immediate_safe_transfer": true,
                  "can_wait_for_combined_mission": false,
                  "handling_reason": "Can rescue ngay",
                  "required_supplies": [],
                  "required_teams": []
                },
                {
                  "sos_request_id": 22,
                  "summary": "Can tiep te nuoc uong",
                  "priority": "High",
                  "needs_immediate_safe_transfer": false,
                  "can_wait_for_combined_mission": true,
                  "handling_reason": "Co the di cung route",
                  "required_supplies": [
                    { "item_name": "Nuoc uong dong chai", "quantity": 8, "unit": "chai" }
                  ],
                  "required_teams": []
                }
              ]
            }
            """,
            ["depot-prefer-draft"] = """
            {
              "activities": [
                {
                  "activity_key": "collect-22",
                  "step": 1,
                  "activity_type": "COLLECT_SUPPLIES",
                  "description": "Lay nuoc tai Kho Hue",
                  "priority": "High",
                  "estimated_time": "20 phut",
                  "sos_request_id": 22,
                  "depot_id": 9,
                  "depot_name": "Kho Hue",
                  "depot_address": "1 Tran Hung Dao",
                  "supplies_to_collect": [
                    {
                      "item_id": 88,
                      "item_name": "Nuoc uong dong chai",
                      "quantity": 8,
                      "unit": "chai"
                    }
                  ]
                },
                {
                  "activity_key": "deliver-22",
                  "step": 2,
                  "activity_type": "DELIVER_SUPPLIES",
                  "description": "Giao nuoc cho SOS 22",
                  "priority": "High",
                  "estimated_time": "20 phut",
                  "sos_request_id": 22,
                  "depot_id": 9,
                  "depot_name": "Kho Hue",
                  "depot_address": "1 Tran Hung Dao",
                  "supplies_to_collect": [
                    {
                      "item_id": 88,
                      "item_name": "Nuoc uong dong chai",
                      "quantity": 8,
                      "unit": "chai"
                    }
                  ]
                }
              ],
              "special_notes": null,
              "needs_additional_depot": false,
              "supply_shortages": [],
              "confidence_score": 0.87
            }
            """,
            ["team-prefer-draft"] = """
            {
              "activity_assignments": [],
              "additional_activities": [
                {
                  "activity_key": "rescue-11",
                  "step": 3,
                  "activity_type": "RESCUE",
                  "description": "Cuu ho SOS 11",
                  "priority": "Critical",
                  "estimated_time": "30 phut",
                  "sos_request_id": 11,
                  "assembly_point_id": 7,
                  "assembly_point_name": "AP Hue",
                  "assembly_point_latitude": 16.470,
                  "assembly_point_longitude": 107.600
                },
                {
                  "activity_key": "evacuate-11",
                  "step": 4,
                  "activity_type": "EVACUATE",
                  "description": "Dua SOS 11 den AP Hue",
                  "priority": "Critical",
                  "estimated_time": "20 phut",
                  "sos_request_id": 11,
                  "assembly_point_id": 7,
                  "assembly_point_name": "AP Hue",
                  "assembly_point_latitude": 16.470,
                  "assembly_point_longitude": 107.600
                }
              ],
              "ordered_activity_keys": ["rescue-11", "evacuate-11", "collect-22", "deliver-22"],
              "suggested_team": null,
              "special_notes": null,
              "confidence_score": 0.84
            }
            """,
            ["validation-relief-only"] = """
            {
              "mission_title": "Validation kept relief only",
              "mission_type": "MIXED",
              "priority_score": 8.7,
              "severity_level": "Critical",
              "overall_assessment": "Relief branch only",
              "activities": [
                {
                  "step": 1,
                  "activity_type": "COLLECT_SUPPLIES",
                  "description": "Lay nuoc tai Kho Hue",
                  "priority": "High",
                  "estimated_time": "20 phut",
                  "sos_request_id": 22,
                  "depot_id": 9,
                  "depot_name": "Kho Hue",
                  "depot_address": "1 Tran Hung Dao"
                },
                {
                  "step": 2,
                  "activity_type": "DELIVER_SUPPLIES",
                  "description": "Giao nuoc cho SOS 22",
                  "priority": "High",
                  "estimated_time": "20 phut",
                  "sos_request_id": 22
                }
              ],
              "resources": [],
              "estimated_duration": "40 phut",
              "special_notes": null,
              "needs_additional_depot": false,
              "supply_shortages": [],
              "confidence_score": 0.9
            }
            """
        });
        var service = new RescueMissionSuggestionService(
            new StubAiProviderClientFactory(aiClient),
            new AiPromptExecutionSettingsResolver(),
            new RecordingAiConfigRepository(BuildAiConfig()),
            promptRepository,
            new RecordingMissionAiSuggestionRepository(),
            ThrowingProxy<IDepotInventoryRepository>.Create(),
            new StubItemModelMetadataRepository(
                new ItemModelRecord
                {
                    Id = 88,
                    CategoryId = 1,
                    Name = "Nuoc uong dong chai",
                    Unit = "chai",
                    ItemType = "Consumable"
                }),
            new EmptyAssemblyPointRepository(),
            Options.Create(new MissionSuggestionPipelineOptions { UseMissionSuggestionPipeline = true }),
            NullLogger<RescueMissionSuggestionService>.Instance);

        var result = await service.PreviewSuggestionAsync(
            [
                new SosRequestSummary
                {
                    Id = 11,
                    SosType = "Support",
                    RawMessage = "Can nguoi tiep can va dua di",
                    AiAnalysis = new SosRequestAiAnalysisSummary
                    {
                        HasAiAnalysis = true,
                        SuggestedPriority = "Critical",
                        NeedsImmediateSafeTransfer = true,
                        CanWaitForCombinedMission = false
                    }
                },
                new SosRequestSummary
                {
                    Id = 22,
                    SosType = "Support",
                    RawMessage = "Can tiep te nuoc uong",
                    AiAnalysis = new SosRequestAiAnalysisSummary
                    {
                        HasAiAnalysis = true,
                        SuggestedPriority = "High",
                        NeedsImmediateSafeTransfer = false,
                        CanWaitForCombinedMission = true
                    }
                }
            ],
            [
                new DepotSummary
                {
                    Id = 9,
                    Name = "Kho Hue",
                    Address = "1 Tran Hung Dao",
                    Latitude = 16.463,
                    Longitude = 107.590
                }
            ],
            [],
            isMultiDepotRecommended: false,
            clusterId: 7,
            promptOverride: BuildStagePrompt(99, PromptType.MissionPlanValidation, "validation-relief-only", isActive: false),
            aiConfigOverride: BuildAiConfig(model: "shared-preview-model"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Validation stage failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.NeedsManualReview);
        Assert.Empty(result.SuggestedActivities);
        Assert.NotNull(result.PipelineMetadata);
        Assert.Equal("failed", result.PipelineMetadata!.PipelineStatus);
        Assert.Null(result.PipelineMetadata.FinalResultSource);
        Assert.False(result.PipelineMetadata.UsedLegacyFallback);
    }

    [Fact]
    public async Task PreviewSuggestionAsync_PipelineStageFailure_DoesNotFallbackToLegacy()
    {
        var promptRepository = new RecordingPromptRepository(
        [
            BuildStagePrompt(4, PromptType.MissionRequirementsAssessment, "requirements-partial"),
            BuildStagePrompt(5, PromptType.MissionDepotPlanning, "depot-partial"),
            BuildStagePrompt(6, PromptType.MissionTeamPlanning, "team-missing"),
            BuildStagePrompt(7, PromptType.MissionPlanValidation, "validation-unused"),
            BuildStagePrompt(8, PromptType.MissionPlanning, "active-legacy-empty")
        ]);
        var aiClient = new PipelineStubAiProviderClient(new Dictionary<string, string>
        {
            ["requirements-partial"] = """
            {
              "suggested_mission_title": "Pipeline partial mission",
              "suggested_mission_type": "MIXED",
              "suggested_priority_score": 9.2,
              "suggested_severity_level": "Critical",
              "overall_assessment": "Partial pipeline output",
              "estimated_duration": "1 gio 10 phut",
              "special_notes": "Canh bao tach cluster.",
              "split_cluster_recommended": true,
              "split_cluster_reason": "Rescue khan cap dang di cung nhanh cuu tro.",
              "needs_additional_depot": false,
              "supply_shortages": [],
              "confidence_score": 0.9,
              "suggested_resources": [],
              "sos_requirements": [
                {
                  "sos_request_id": 11,
                  "summary": "Can cuu ho gap",
                  "priority": "Critical",
                  "needs_immediate_safe_transfer": true,
                  "can_wait_for_combined_mission": false,
                  "handling_reason": "Can dua den noi an toan ngay.",
                  "required_supplies": [],
                  "required_teams": [
                    { "team_type": "Rescue", "quantity": 1, "reason": "Can cuu ho nan nhan" }
                  ]
                },
                {
                  "sos_request_id": 22,
                  "summary": "Can tiep te nuoc va luong thuc",
                  "priority": "High",
                  "needs_immediate_safe_transfer": false,
                  "can_wait_for_combined_mission": true,
                  "handling_reason": "Nhanh cuu tro co the di cung route an toan.",
                  "required_supplies": [
                    { "item_name": "Nuoc sach", "quantity": 10, "unit": "chai" }
                  ],
                  "required_teams": []
                }
              ]
            }
            """,
            ["depot-partial"] = """
            {
              "activities": [
                {
                  "activity_key": "collect-22",
                  "step": 1,
                  "activity_type": "COLLECT_SUPPLIES",
                  "description": "Lay nuoc sach tai Kho Hue",
                  "priority": "High",
                  "estimated_time": "20 phut",
                  "sos_request_id": 22,
                  "depot_id": 9,
                  "depot_name": "Kho Hue",
                  "depot_address": "1 Tran Hung Dao",
                  "supplies_to_collect": [
                    {
                      "item_id": 88,
                      "item_name": "Nuoc uong dong chai",
                      "quantity": 10,
                      "unit": "chai"
                    }
                  ]
                },
                {
                  "activity_key": "deliver-22",
                  "step": 2,
                  "activity_type": "DELIVER_SUPPLIES",
                  "description": "Giao nuoc sach cho SOS 22",
                  "priority": "High",
                  "estimated_time": "20 phut",
                  "sos_request_id": 22,
                  "depot_id": 9,
                  "depot_name": "Kho Hue",
                  "depot_address": "1 Tran Hung Dao",
                  "supplies_to_collect": [
                    {
                      "item_id": 88,
                      "item_name": "Nuoc uong dong chai",
                      "quantity": 10,
                      "unit": "chai"
                    }
                  ]
                }
              ],
              "special_notes": null,
              "needs_additional_depot": false,
              "supply_shortages": [],
              "confidence_score": 0.85
            }
            """,
            ["active-legacy-empty"] = """
            {
              "mission_title": "Legacy empty mission",
              "mission_type": "MIXED",
              "priority_score": 8.9,
              "severity_level": "Critical",
              "overall_assessment": "Legacy returned no activities",
              "activities": [],
              "resources": [],
              "estimated_duration": "50 phut",
              "special_notes": "Nen tach cluster.",
              "needs_additional_depot": false,
              "supply_shortages": [],
              "confidence_score": 0.9
            }
            """
        });
        var service = new RescueMissionSuggestionService(
            new StubAiProviderClientFactory(aiClient),
            new AiPromptExecutionSettingsResolver(),
            new RecordingAiConfigRepository(BuildAiConfig()),
            promptRepository,
            new RecordingMissionAiSuggestionRepository(),
            ThrowingProxy<IDepotInventoryRepository>.Create(),
            new StubItemModelMetadataRepository(
                new ItemModelRecord
                {
                    Id = 88,
                    CategoryId = 1,
                    Name = "Nuoc uong dong chai",
                    Unit = "chai",
                    ItemType = "Consumable"
                }),
            new EmptyAssemblyPointRepository(),
            Options.Create(new MissionSuggestionPipelineOptions { UseMissionSuggestionPipeline = true }),
            NullLogger<RescueMissionSuggestionService>.Instance);

        var result = await service.PreviewSuggestionAsync(
            [
                new SosRequestSummary
                {
                    Id = 11,
                    SosType = "Rescue",
                    RawMessage = "Nan nhan can dua den noi an toan ngay",
                    AiAnalysis = new SosRequestAiAnalysisSummary
                    {
                        HasAiAnalysis = true,
                        SuggestedPriority = "Critical",
                        NeedsImmediateSafeTransfer = true,
                        CanWaitForCombinedMission = false
                    }
                },
                new SosRequestSummary
                {
                    Id = 22,
                    SosType = "Relief",
                    RawMessage = "Can tiep te nuoc va luong thuc",
                    AiAnalysis = new SosRequestAiAnalysisSummary
                    {
                        HasAiAnalysis = true,
                        SuggestedPriority = "High",
                        NeedsImmediateSafeTransfer = false,
                        CanWaitForCombinedMission = true
                    }
                }
            ],
            [
                new DepotSummary
                {
                    Id = 9,
                    Name = "Kho Hue",
                    Address = "1 Tran Hung Dao",
                    Latitude = 16.463,
                    Longitude = 107.590
                }
            ],
            [],
            isMultiDepotRecommended: false,
            clusterId: 7,
            promptOverride: BuildStagePrompt(99, PromptType.MissionRequirementsAssessment, "requirements-partial", isActive: false),
            aiConfigOverride: BuildAiConfig(model: "shared-preview-model"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Team stage failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.NeedsManualReview);
        Assert.Empty(result.SuggestedActivities);
        Assert.NotNull(result.PipelineMetadata);
        Assert.Equal("failed", result.PipelineMetadata!.PipelineStatus);
        Assert.Null(result.PipelineMetadata.FinalResultSource);
        Assert.False(result.PipelineMetadata.UsedLegacyFallback);
    }

    [Fact]
    public async Task PreviewSuggestionAsync_MissionPlanningPrompt_UnresolvedGenericSupplyMovesToShortage()
    {
        var service = new RescueMissionSuggestionService(
            new StubAiProviderClientFactory(new LegacyStubAiProviderClient(
                """
                {
                  "mission_title": "Preview plan",
                  "mission_type": "SUPPLY",
                  "priority_score": 7.8,
                  "severity_level": "High",
                  "overall_assessment": "Can tiep te",
                  "activities": [
                    {
                      "step": 1,
                      "activity_type": "COLLECT_SUPPLIES",
                      "description": "Chuan bi do tiep te",
                      "priority": "High",
                      "estimated_time": "20 phut",
                      "sos_request_id": 1,
                      "depot_id": 9,
                      "depot_name": "Kho Preview",
                      "depot_address": "1 Preview Street",
                      "supplies_to_collect": [
                        {
                          "item_name": "Nhu yeu pham thiet yeu",
                          "quantity": 1,
                          "unit": "goi"
                        }
                      ]
                    },
                    {
                      "step": 2,
                      "activity_type": "DELIVER_SUPPLIES",
                      "description": "Giao do cho SOS 1",
                      "priority": "High",
                      "estimated_time": "20 phut",
                      "sos_request_id": 1,
                      "depot_id": 9,
                      "depot_name": "Kho Preview",
                      "depot_address": "1 Preview Street",
                      "supplies_to_collect": [
                        {
                          "item_name": "Nhu yeu pham thiet yeu",
                          "quantity": 1,
                          "unit": "goi"
                        }
                      ]
                    }
                  ],
                  "resources": [],
                  "estimated_duration": "40 phut",
                  "special_notes": null,
                  "needs_additional_depot": false,
                  "supply_shortages": [],
                  "confidence_score": 0.82
                }
                """)),
            new AiPromptExecutionSettingsResolver(),
            new RecordingAiConfigRepository(BuildAiConfig()),
            ThrowingProxy<IPromptRepository>.Create(),
            new RecordingMissionAiSuggestionRepository(),
            ThrowingProxy<IDepotInventoryRepository>.Create(),
            ThrowingProxy<IItemModelMetadataRepository>.Create(),
            new EmptyAssemblyPointRepository(),
            Options.Create(new MissionSuggestionPipelineOptions { UseMissionSuggestionPipeline = true }),
            NullLogger<RescueMissionSuggestionService>.Instance);

        var result = await service.PreviewSuggestionAsync(
            [new SosRequestSummary { Id = 1, RawMessage = "Can tiep te" }],
            [
                new DepotSummary
                {
                    Id = 9,
                    Name = "Kho Preview",
                    Address = "1 Preview Street",
                    Latitude = 16.463,
                    Longitude = 107.590
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

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(result.NeedsManualReview);
        Assert.Single(result.SupplyShortages);
        Assert.DoesNotContain(
            result.SuggestedActivities.SelectMany(activity => activity.SuppliesToCollect ?? []),
            supply => string.Equals(supply.ItemName, "Nhu yeu pham thiet yeu", StringComparison.OrdinalIgnoreCase));
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

    private sealed class StubAiProviderClientFactory(IAiProviderClient client) : IAiProviderClientFactory
    {
        public IAiProviderClient GetClient(AiProvider provider) => client;
    }

    private sealed class LegacyStubAiProviderClient(string? responseText = null) : IAiProviderClient
    {
        public AiProvider Provider => AiProvider.Gemini;

        public Task<AiCompletionResponse> CompleteAsync(
            AiCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiCompletionResponse
            {
                Text = responseText ?? """
                {
                  "mission_title": "Preview plan",
                  "mission_type": "MIXED",
                  "priority_score": 7.5,
                  "severity_level": "Moderate",
                  "overall_assessment": "Preview only",
                  "activities": [
                    {
                      "step": 1,
                      "activity_type": "RESCUE",
                      "description": "Dua nan nhan den diem an toan",
                      "estimated_time": "20 phut",
                      "sos_request_id": 1
                    }
                  ],
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

    private sealed class PipelineStubAiProviderClient(Dictionary<string, string>? stageResponses = null) : IAiProviderClient
    {
        public AiProvider Provider => AiProvider.Gemini;
        public List<string> StageMarkers { get; } = [];
        private readonly IReadOnlyDictionary<string, string> _stageResponses = stageResponses ?? new Dictionary<string, string>
        {
            ["active-requirements"] = """
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
            ["override-depot"] = """
            {
              "activities": [
                {
                  "activity_key": "collect-1",
                  "step": 1,
                  "activity_type": "COLLECT_SUPPLIES",
                  "description": "Lay do tu kho preview",
                  "priority": "High",
                  "estimated_time": "20 phut",
                  "sos_request_id": 1,
                  "depot_id": 9,
                  "depot_name": "Kho Preview",
                  "depot_address": "1 Preview Street",
                  "supplies_to_collect": [
                    {
                      "item_id": 501,
                      "item_name": "Nuoc uong dong chai",
                      "quantity": 2,
                      "unit": "chai"
                    }
                  ]
                },
                {
                  "activity_key": "deliver-1",
                  "step": 2,
                  "activity_type": "DELIVER_SUPPLIES",
                  "description": "Giao do cho SOS 1",
                  "priority": "High",
                  "estimated_time": "20 phut",
                  "sos_request_id": 1,
                  "depot_id": 9,
                  "depot_name": "Kho Preview",
                  "depot_address": "1 Preview Street",
                  "supplies_to_collect": [
                    {
                      "item_id": 501,
                      "item_name": "Nuoc uong dong chai",
                      "quantity": 2,
                      "unit": "chai"
                    }
                  ]
                }
              ],
              "special_notes": null,
              "needs_additional_depot": false,
              "supply_shortages": [],
              "confidence_score": 0.8
            }
            """,
            ["active-team"] = """
            {
              "activity_assignments": [],
              "additional_activities": [],
              "ordered_activity_keys": ["collect-1", "deliver-1"],
              "suggested_team": null,
              "special_notes": null,
              "confidence_score": 0.8
            }
            """,
            ["active-validation"] = """
            {
              "mission_title": "Pipeline preview",
              "mission_type": "SUPPLY",
              "priority_score": 6.5,
              "severity_level": "Moderate",
              "overall_assessment": "Preview only",
              "activities": [
                {
                  "step": 1,
                  "activity_type": "COLLECT_SUPPLIES",
                  "description": "Lay do tu kho preview",
                  "priority": "High",
                  "estimated_time": "20 phut",
                  "sos_request_id": 1,
                  "depot_id": 9,
                  "depot_name": "Kho Preview",
                  "depot_address": "1 Preview Street",
                  "supplies_to_collect": [
                    {
                      "item_id": 501,
                      "item_name": "Nuoc uong dong chai",
                      "quantity": 2,
                      "unit": "chai"
                    }
                  ]
                },
                {
                  "step": 2,
                  "activity_type": "DELIVER_SUPPLIES",
                  "description": "Giao do cho SOS 1",
                  "priority": "High",
                  "estimated_time": "20 phut",
                  "sos_request_id": 1,
                  "depot_id": 9,
                  "depot_name": "Kho Preview",
                  "depot_address": "1 Preview Street",
                  "supplies_to_collect": [
                    {
                      "item_id": 501,
                      "item_name": "Nuoc uong dong chai",
                      "quantity": 2,
                      "unit": "chai"
                    }
                  ]
                }
              ],
              "resources": [],
              "estimated_duration": "40 phut",
              "special_notes": null,
              "needs_additional_depot": false,
              "supply_shortages": [],
              "confidence_score": 0.9
            }
            """
        };

        public Task<AiCompletionResponse> CompleteAsync(
            AiCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            var marker = ExtractMarker(request.SystemPrompt);
            StageMarkers.Add(marker);

            if (!_stageResponses.TryGetValue(marker, out var text))
                throw new InvalidOperationException($"Unexpected stage marker {marker}.");

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

    private sealed class StubItemModelMetadataRepository(params ItemModelRecord[] items) : IItemModelMetadataRepository
    {
        private readonly Dictionary<int, ItemModelRecord> _items = items.ToDictionary(item => item.Id);

        public Task<List<RESQ.Application.Common.Models.MetadataDto>> GetAllForMetadataAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<List<RESQ.Application.Common.Models.MetadataDto>>([]);

        public Task<List<RESQ.Application.Common.Models.MetadataDto>> GetByCategoryCodeAsync(
            RESQ.Domain.Enum.Logistics.ItemCategoryCode categoryCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult<List<RESQ.Application.Common.Models.MetadataDto>>([]);

        public Task<List<RESQ.Application.Services.DonationImportItemInfo>> GetAllForDonationTemplateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<List<RESQ.Application.Services.DonationImportItemInfo>>([]);

        public Task<List<RESQ.Application.Services.DonationImportTargetGroupInfo>> GetAllTargetGroupsForTemplateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<List<RESQ.Application.Services.DonationImportTargetGroupInfo>>([]);

        public Task<Dictionary<int, ItemModelRecord>> GetByIdsAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default)
            => Task.FromResult(ids.Where(id => _items.ContainsKey(id)).ToDictionary(id => id, id => _items[id]));

        public Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> HasInventoryTransactionsAsync(int itemModelId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> UpdateItemModelAsync(ItemModelRecord model, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
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
