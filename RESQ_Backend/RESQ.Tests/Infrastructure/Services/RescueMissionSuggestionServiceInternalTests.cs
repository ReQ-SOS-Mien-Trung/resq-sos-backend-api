using System.Reflection;
using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Infrastructure.Services;

namespace RESQ.Tests.Infrastructure.Services;

public class RescueMissionSuggestionServiceInternalTests
{
    [Fact]
    public void ParseMissionSuggestion_ParsesShortagesAndAdditionalDepotFlag()
    {
        var result = ParseMissionSuggestion(
            """
            {
              "suggested_mission_title": "Mission",
              "needs_additional_depot": true,
              "supply_shortages": [
                {
                  "sos_request_id": 2,
                  "item_id": 7,
                  "item_name": "Nuoc sach",
                  "unit": "chai",
                  "selected_depot_id": 11,
                  "selected_depot_name": "Kho A",
                  "needed_quantity": 20,
                  "available_quantity": 5,
                  "missing_quantity": 15,
                  "notes": "selected depot lacks stock"
                }
              ],
              "confidence_score": 0.8
            }
            """);

        Assert.True(result.NeedsAdditionalDepot);

        var shortage = Assert.Single(result.SupplyShortages);
        Assert.Equal(2, shortage.SosRequestId);
        Assert.Equal(7, shortage.ItemId);
        Assert.Equal("Nuoc sach", shortage.ItemName);
        Assert.Equal(15, shortage.MissingQuantity);
    }

    [Fact]
    public void ParseMissionSuggestion_MediumWarning_MapsToManualReviewWarning()
    {
        var result = ParseMissionSuggestion(
            """
            {
              "mission_title": "Mission",
              "warning_level": "medium",
              "warning_title": "Can xem xet bo sung",
              "warning_message": "Co 2 SOS priority cao can coordinator kiem tra lai route.",
              "warning_related_sos_ids": [11, 12],
              "warning_reason": "Cluster co nhieu diem nguy co dang xem xet.",
              "confidence_score": 0.8
            }
            """);

        Assert.True(result.NeedsManualReview);
        Assert.Contains("Can xem xet bo sung", result.LowConfidenceWarning);
        Assert.Contains("#11", result.LowConfidenceWarning);
        Assert.Contains("#12", result.LowConfidenceWarning);
    }

    [Fact]
    public void ParseMissionSuggestion_StrongSafetyWarning_MapsToMixedWarning()
    {
        var result = ParseMissionSuggestion(
            """
            {
              "mission_title": "Mission",
              "warning_level": "strong",
              "warning_title": "Mixed route khong an toan",
              "warning_message": "Cluster dang ghep nhanh rescue va relief cho SOS critical.",
              "warning_related_sos_ids": [11, 22],
              "warning_reason": "Can uu tien safe transfer truoc khi tiep te.",
              "confidence_score": 0.8
            }
            """);

        Assert.True(result.NeedsManualReview);
        Assert.Contains("Mixed route khong an toan", result.MixedRescueReliefWarning);
        Assert.Contains("#11", result.MixedRescueReliefWarning);
        Assert.Contains("#22", result.MixedRescueReliefWarning);
    }

    [Fact]
    public void DeserializePipelineFragment_Requirements_ToleratesStringSupplyShortages()
    {
        var fragment = DeserializePipelineFragment<MissionRequirementsFragment>(
            """
            {
              "suggested_mission_title": "Mission",
              "warning_level": "light",
              "supply_shortages": ["Nuoc sach"],
              "confidence_score": 0.8,
              "sos_requirements": [
                {
                  "sos_request_id": 11,
                  "summary": "Can nuoc",
                  "priority": "High",
                  "required_supplies": [],
                  "required_teams": []
                }
              ]
            }
            """);

        var shortage = Assert.Single(fragment.SupplyShortages);
        Assert.Equal("Nuoc sach", shortage.ItemName);
        Assert.Equal(1, shortage.NeededQuantity);
        Assert.Equal(1, shortage.MissingQuantity);
    }

    [Fact]
    public void DeserializePipelineFragment_Depot_WrapsSingletonActivityAndSupplyObject()
    {
        var fragment = DeserializePipelineFragment<MissionDepotFragment>(
            """
            {
              "activities": {
                "activity_key": "collect-1",
                "step": "1",
                "activity_type": "COLLECT_SUPPLIES",
                "description": "Lay ao phao",
                "sos_request_id": "1",
                "depot_id": "9",
                "depot_name": "Kho Preview",
                "supplies_to_collect": {
                  "item_id": "501",
                  "item_name": "Ao phao",
                  "quantity": "2",
                  "unit": "cai"
                }
              },
              "confidence_score": "0.8"
            }
            """);

        var activity = Assert.Single(fragment.Activities);
        Assert.Equal("collect-1", activity.ActivityKey);
        Assert.Equal(1, activity.Step);
        Assert.Equal(9, activity.DepotId);
        var supply = Assert.Single(activity.SuppliesToCollect!);
        Assert.Equal(501, supply.ItemId);
        Assert.Equal(2, supply.Quantity);
    }

    [Fact]
    public void DeserializePipelineFragment_Team_ToleratesNonObjectTopLevelSuggestedTeam()
    {
        var fragment = DeserializePipelineFragment<MissionTeamFragment>(
            """
            {
              "activity_assignments": [],
              "additional_activities": [],
              "ordered_activity_keys": ["collect-1"],
              "suggested_team": [],
              "confidence_score": 0.8
            }
            """);

        Assert.Null(fragment.SuggestedTeam);
        Assert.Single(fragment.OrderedActivityKeys);
        Assert.Equal("collect-1", fragment.OrderedActivityKeys[0]);
    }

    [Fact]
    public void ValidateTeamFragment_FillsMissingOrderedActivityKeysAndDropsUnknownAssignments()
    {
        var depot = new MissionDepotFragment
        {
            Activities =
            [
                new MissionActivityFragment
                {
                    ActivityKey = "collect-22",
                    Step = 1,
                    ActivityType = "COLLECT_SUPPLIES",
                    DepotId = 9,
                    SuppliesToCollect =
                    [
                        new SupplyToCollectDto
                        {
                            ItemId = 88,
                            ItemName = "Nuoc sach",
                            Quantity = 10,
                            Unit = "chai"
                        }
                    ]
                },
                new MissionActivityFragment
                {
                    ActivityKey = "deliver-22",
                    Step = 2,
                    ActivityType = "DELIVER_SUPPLIES",
                    DepotId = 9,
                    SuppliesToCollect =
                    [
                        new SupplyToCollectDto
                        {
                            ItemId = 88,
                            ItemName = "Nuoc sach",
                            Quantity = 10,
                            Unit = "chai"
                        }
                    ]
                }
            ]
        };
        var team = new MissionTeamFragment
        {
            ActivityAssignments =
            [
                new MissionActivityAssignmentFragment
                {
                    ActivityKey = "collect-22",
                    SuggestedTeam = new SuggestedTeamDto { TeamId = 21, TeamName = "Team A" }
                },
                new MissionActivityAssignmentFragment
                {
                    ActivityKey = "unknown-key",
                    SuggestedTeam = new SuggestedTeamDto { TeamId = 22, TeamName = "Team B" }
                }
            ],
            OrderedActivityKeys = []
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "ValidateTeamFragment", team, depot);

        Assert.Single(team.ActivityAssignments);
        Assert.Equal(["collect-22", "deliver-22"], team.OrderedActivityKeys);
    }

    [Fact]
    public void AssessMissionActivityRoute_AllowsCollectBeforeUrgentRescueWithoutRequiresSupplyFlag()
    {
        var team = new SuggestedTeamDto { TeamId = 21, TeamName = "Team A" };
        var activities = new List<SuggestedActivityDto>
        {
            new()
            {
                Step = 1,
                ActivityType = "COLLECT_SUPPLIES",
                SosRequestId = 96,
                DepotId = 1,
                DepotName = "Kho A",
                SuggestedTeam = team,
                SuppliesToCollect =
                [
                    new SupplyToCollectDto
                    {
                        ItemId = 15,
                        ItemName = "Ao phao",
                        Quantity = 2,
                        Unit = "cai"
                    }
                ]
            },
            new()
            {
                Step = 2,
                ActivityType = "RESCUE",
                SosRequestId = 96,
                SuggestedTeam = team
            },
            new()
            {
                Step = 3,
                ActivityType = "EVACUATE",
                SosRequestId = 96,
                SuggestedTeam = team
            }
        };
        var sosRequests = new List<SosRequestSummary>
        {
            new()
            {
                Id = 96,
                SosType = "RESCUE",
                PriorityLevel = "Critical",
                AiAnalysis = new SosRequestAiAnalysisSummary
                {
                    HasAiAnalysis = true,
                    SuggestedPriority = "Critical",
                    NeedsImmediateSafeTransfer = true,
                    CanWaitForCombinedMission = false
                }
            }
        };
        var requirements = new MissionRequirementsFragment
        {
            SosRequirements =
            [
                new MissionSosRequirementFragment
                {
                    SosRequestId = 96,
                    Priority = "Critical",
                    NeedsImmediateSafeTransfer = true,
                    CanWaitForCombinedMission = false,
                    RequiresSupplyBeforeRescue = false
                }
            ]
        };

        var failure = InvokeStaticResult<string?>(
            nameof(RescueMissionSuggestionService),
            "AssessMissionActivityRoute",
            activities,
            sosRequests,
            requirements);

        Assert.Null(failure);
    }

    [Fact]
    public void AssessMissionActivityRoute_AllowsDeliverBeforeUrgentRescueForSameSos()
    {
        var team = new SuggestedTeamDto { TeamId = 21, TeamName = "Team A" };
        var activities = new List<SuggestedActivityDto>
        {
            new()
            {
                Step = 1,
                ActivityType = "COLLECT_SUPPLIES",
                SosRequestId = 44,
                DepotId = 1,
                DepotName = "Kho A",
                SuggestedTeam = team,
                SuppliesToCollect =
                [
                    new SupplyToCollectDto
                    {
                        ItemId = 15,
                        ItemName = "Ao phao",
                        Quantity = 3,
                        Unit = "cai"
                    }
                ]
            },
            new()
            {
                Step = 2,
                ActivityType = "DELIVER_SUPPLIES",
                SosRequestId = 44,
                DepotId = 1,
                DepotName = "Kho A",
                SuggestedTeam = team,
                SuppliesToCollect =
                [
                    new SupplyToCollectDto
                    {
                        ItemId = 15,
                        ItemName = "Ao phao",
                        Quantity = 3,
                        Unit = "cai"
                    }
                ]
            },
            new()
            {
                Step = 3,
                ActivityType = "RESCUE",
                SosRequestId = 44,
                SuggestedTeam = team
            },
            new()
            {
                Step = 4,
                ActivityType = "EVACUATE",
                SosRequestId = 44,
                SuggestedTeam = team
            }
        };
        var sosRequests = new List<SosRequestSummary>
        {
            new()
            {
                Id = 44,
                SosType = "RESCUE",
                PriorityLevel = "Critical",
                AiAnalysis = new SosRequestAiAnalysisSummary
                {
                    HasAiAnalysis = true,
                    SuggestedPriority = "Critical",
                    NeedsImmediateSafeTransfer = true,
                    CanWaitForCombinedMission = false
                }
            }
        };
        var requirements = new MissionRequirementsFragment
        {
            SosRequirements =
            [
                new MissionSosRequirementFragment
                {
                    SosRequestId = 44,
                    Priority = "Critical",
                    NeedsImmediateSafeTransfer = true,
                    CanWaitForCombinedMission = false,
                    RequiresSupplyBeforeRescue = false
                }
            ]
        };

        var failure = InvokeStaticResult<string?>(
            nameof(RescueMissionSuggestionService),
            "AssessMissionActivityRoute",
            activities,
            sosRequests,
            requirements);

        Assert.Null(failure);
    }

    [Fact]
    public void AssessMissionActivityRoute_AllowsUrgentDeliveryForAnotherUrgentSosBeforeTargetRescueStarts()
    {
        var team = new SuggestedTeamDto { TeamId = 21, TeamName = "Team A" };
        var activities = new List<SuggestedActivityDto>
        {
            new()
            {
                Step = 1,
                ActivityType = "COLLECT_SUPPLIES",
                SosRequestId = 86,
                DepotId = 1,
                DepotName = "Kho A",
                SuggestedTeam = team,
                SuppliesToCollect =
                [
                    new SupplyToCollectDto
                    {
                        ItemId = 15,
                        ItemName = "Ao phao",
                        Quantity = 4,
                        Unit = "cai"
                    }
                ]
            },
            new()
            {
                Step = 2,
                ActivityType = "DELIVER_SUPPLIES",
                SosRequestId = 86,
                DepotId = 1,
                DepotName = "Kho A",
                SuggestedTeam = team,
                SuppliesToCollect =
                [
                    new SupplyToCollectDto
                    {
                        ItemId = 15,
                        ItemName = "Ao phao",
                        Quantity = 4,
                        Unit = "cai"
                    }
                ]
            },
            new()
            {
                Step = 3,
                ActivityType = "RESCUE",
                SosRequestId = 86,
                SuggestedTeam = team
            },
            new()
            {
                Step = 4,
                ActivityType = "EVACUATE",
                SosRequestId = 85,
                SuggestedTeam = team
            }
        };
        var sosRequests = new List<SosRequestSummary>
        {
            new()
            {
                Id = 85,
                SosType = "RESCUE",
                PriorityLevel = "Critical",
                AiAnalysis = new SosRequestAiAnalysisSummary
                {
                    HasAiAnalysis = true,
                    SuggestedPriority = "Critical",
                    NeedsImmediateSafeTransfer = true,
                    CanWaitForCombinedMission = false
                }
            },
            new()
            {
                Id = 86,
                SosType = "RESCUE",
                PriorityLevel = "Critical",
                AiAnalysis = new SosRequestAiAnalysisSummary
                {
                    HasAiAnalysis = true,
                    SuggestedPriority = "Critical",
                    NeedsImmediateSafeTransfer = true,
                    CanWaitForCombinedMission = false
                }
            }
        };
        var requirements = new MissionRequirementsFragment
        {
            SosRequirements =
            [
                new MissionSosRequirementFragment
                {
                    SosRequestId = 85,
                    Priority = "Critical",
                    NeedsImmediateSafeTransfer = true,
                    CanWaitForCombinedMission = false,
                    RequiredSupplies = [],
                    RequiredTeams = []
                },
                new MissionSosRequirementFragment
                {
                    SosRequestId = 86,
                    Priority = "Critical",
                    NeedsImmediateSafeTransfer = true,
                    CanWaitForCombinedMission = false,
                    RequiredSupplies = [],
                    RequiredTeams = []
                }
            ]
        };

        var failure = InvokeStaticResult<string?>(
            nameof(RescueMissionSuggestionService),
            "AssessMissionActivityRoute",
            activities,
            sosRequests,
            requirements);

        Assert.Null(failure);
    }

    [Fact]
    public void AssessExecutableMissionResult_BackfillsMissingSupplyRouteDetailsFromExpectedActivities()
    {
        var team = new SuggestedTeamDto { TeamId = 21, TeamName = "Team A" };
        var expectedActivities = new List<SuggestedActivityDto>
        {
            new()
            {
                Step = 1,
                ActivityType = "COLLECT_SUPPLIES",
                SosRequestId = 49,
                DepotId = 5,
                DepotName = "Kho Hue",
                SuggestedTeam = team,
                SuppliesToCollect =
                [
                    new SupplyToCollectDto
                    {
                        ItemId = 15,
                        ItemName = "Ao phao",
                        Quantity = 2,
                        Unit = "cai"
                    }
                ]
            },
            new()
            {
                Step = 2,
                ActivityType = "DELIVER_SUPPLIES",
                SosRequestId = 49,
                DepotId = 5,
                DepotName = "Kho Hue",
                SuggestedTeam = team,
                SuppliesToCollect =
                [
                    new SupplyToCollectDto
                    {
                        ItemId = 15,
                        ItemName = "Ao phao",
                        Quantity = 2,
                        Unit = "cai"
                    }
                ]
            },
            new()
            {
                Step = 3,
                ActivityType = "RESCUE",
                SosRequestId = 49,
                SuggestedTeam = team
            },
            new()
            {
                Step = 4,
                ActivityType = "EVACUATE",
                SosRequestId = 49,
                SuggestedTeam = team
            }
        };
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto
                {
                    Step = 1,
                    ActivityType = "COLLECT_SUPPLIES",
                    SosRequestId = 49,
                    DepotId = 5,
                    DepotName = "Kho Hue",
                    SuggestedTeam = team,
                    SuppliesToCollect =
                    [
                        new SupplyToCollectDto
                        {
                            ItemId = 15,
                            ItemName = "Ao phao",
                            Quantity = 2,
                            Unit = "cai"
                        }
                    ]
                },
                new SuggestedActivityDto
                {
                    Step = 2,
                    ActivityType = "DELIVER_SUPPLIES",
                    SosRequestId = 49,
                    SuggestedTeam = team
                },
                new SuggestedActivityDto
                {
                    Step = 3,
                    ActivityType = "RESCUE",
                    SosRequestId = 49,
                    SuggestedTeam = team
                },
                new SuggestedActivityDto
                {
                    Step = 4,
                    ActivityType = "EVACUATE",
                    SosRequestId = 49,
                    SuggestedTeam = team
                }
            ]
        };
        var sosRequests = new List<SosRequestSummary>
        {
            new()
            {
                Id = 49,
                SosType = "RESCUE",
                PriorityLevel = "Critical",
                AiAnalysis = new SosRequestAiAnalysisSummary
                {
                    HasAiAnalysis = true,
                    SuggestedPriority = "Critical",
                    NeedsImmediateSafeTransfer = true,
                    CanWaitForCombinedMission = false
                }
            }
        };
        var requirements = new MissionRequirementsFragment
        {
            SosRequirements =
            [
                new MissionSosRequirementFragment
                {
                    SosRequestId = 49,
                    Priority = "Critical",
                    NeedsImmediateSafeTransfer = true,
                    CanWaitForCombinedMission = false,
                    RequiresSupplyBeforeRescue = false,
                    RequiredSupplies = [],
                    RequiredTeams = []
                }
            ]
        };

        var assessment = InvokeStaticResult<object>(
            nameof(RescueMissionSuggestionService),
            "AssessExecutableMissionResult",
            result,
            sosRequests,
            expectedActivities,
            requirements);

        Assert.NotNull(assessment);
        var isExecutable = (bool)assessment!.GetType().GetProperty("IsExecutable")!.GetValue(assessment)!;
        Assert.True(isExecutable);

        var deliveredSupply = Assert.Single(result.SuggestedActivities[1].SuppliesToCollect!);
        Assert.Equal(5, result.SuggestedActivities[1].DepotId);
        Assert.Equal("Kho Hue", result.SuggestedActivities[1].DepotName);
        Assert.Equal(15, deliveredSupply.ItemId);
        Assert.Equal(2, deliveredSupply.Quantity);
    }

    [Fact]
    public void AssessMissionActivityRoute_AllowsUrgentRescueWithoutEvacuateBeforeLaterReliefWork()
    {
        var team = new SuggestedTeamDto { TeamId = 21, TeamName = "Team A" };
        var activities = new List<SuggestedActivityDto>
        {
            new()
            {
                Step = 1,
                ActivityType = "RESCUE",
                SosRequestId = 93,
                SuggestedTeam = team
            },
            new()
            {
                Step = 2,
                ActivityType = "COLLECT_SUPPLIES",
                SosRequestId = 22,
                DepotId = 1,
                DepotName = "Kho A",
                SuggestedTeam = team,
                SuppliesToCollect =
                [
                    new SupplyToCollectDto
                    {
                        ItemId = 15,
                        ItemName = "Nuoc sach",
                        Quantity = 5,
                        Unit = "chai"
                    }
                ]
            },
            new()
            {
                Step = 3,
                ActivityType = "DELIVER_SUPPLIES",
                SosRequestId = 22,
                DepotId = 1,
                DepotName = "Kho A",
                SuggestedTeam = team,
                SuppliesToCollect =
                [
                    new SupplyToCollectDto
                    {
                        ItemId = 15,
                        ItemName = "Nuoc sach",
                        Quantity = 5,
                        Unit = "chai"
                    }
                ]
            }
        };
        var sosRequests = new List<SosRequestSummary>
        {
            new()
            {
                Id = 93,
                SosType = "RESCUE",
                PriorityLevel = "Critical",
                AiAnalysis = new SosRequestAiAnalysisSummary
                {
                    HasAiAnalysis = true,
                    SuggestedPriority = "Critical",
                    NeedsImmediateSafeTransfer = true,
                    CanWaitForCombinedMission = false
                }
            },
            new()
            {
                Id = 22,
                SosType = "RELIEF",
                PriorityLevel = "High",
                AiAnalysis = new SosRequestAiAnalysisSummary
                {
                    HasAiAnalysis = true,
                    SuggestedPriority = "High",
                    NeedsImmediateSafeTransfer = false,
                    CanWaitForCombinedMission = true
                }
            }
        };
        var requirements = new MissionRequirementsFragment
        {
            SosRequirements =
            [
                new MissionSosRequirementFragment
                {
                    SosRequestId = 93,
                    Priority = "Critical",
                    NeedsImmediateSafeTransfer = true,
                    CanWaitForCombinedMission = false,
                    RequiredSupplies = [],
                    RequiredTeams = []
                },
                new MissionSosRequirementFragment
                {
                    SosRequestId = 22,
                    Priority = "High",
                    NeedsImmediateSafeTransfer = false,
                    CanWaitForCombinedMission = true,
                    RequiredSupplies = [],
                    RequiredTeams = []
                }
            ]
        };

        var failure = InvokeStaticResult<string?>(
            nameof(RescueMissionSuggestionService),
            "AssessMissionActivityRoute",
            activities,
            sosRequests,
            requirements);

        Assert.Null(failure);
    }

    [Fact]
    public void NormalizeSupplyShortages_FillsDepotMissingQuantityAndCoordinatorNote()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto
                {
                    Step = 1,
                    ActivityType = "COLLECT_SUPPLIES",
                    DepotId = 9,
                    DepotName = "Kho A"
                }
            ],
            SupplyShortages =
            [
                new SupplyShortageDto
                {
                    SosRequestId = 5,
                    ItemName = "Nuoc sach",
                    NeededQuantity = 12,
                    AvailableQuantity = 4,
                    MissingQuantity = 0
                }
            ]
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "NormalizeSupplyShortages", result);

        Assert.True(result.NeedsAdditionalDepot);

        var shortage = Assert.Single(result.SupplyShortages);
        Assert.Equal(9, shortage.SelectedDepotId);
        Assert.Equal("Kho A", shortage.SelectedDepotName);
        Assert.Equal(8, shortage.MissingQuantity);
        Assert.Contains("Coordinator", result.SpecialNotes);
        Assert.Contains("Nuoc sach", result.SpecialNotes);
    }

    [Fact]
    public void ReconcileSupplyShortagesWithInventory_ClearsGenericMedicalShortageWhenDepotHasMatchingStock()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto
                {
                    Step = 1,
                    ActivityType = "COLLECT_SUPPLIES",
                    DepotId = 9,
                    DepotName = "Kho A"
                }
            ],
            SupplyShortages =
            [
                new SupplyShortageDto
                {
                    SosRequestId = 5,
                    ItemName = "Thuốc men",
                    Unit = "bộ",
                    NeededQuantity = 1,
                    AvailableQuantity = 0,
                    MissingQuantity = 1
                }
            ]
        };

        var depots = new List<DepotSummary>
        {
            new()
            {
                Id = 9,
                Name = "Kho A",
                Inventories =
                [
                    new DepotInventoryItemDto
                    {
                        ItemId = 33,
                        ItemName = "Bộ sơ cứu cơ bản",
                        Unit = "bộ",
                        AvailableQuantity = 4
                    },
                    new DepotInventoryItemDto
                    {
                        ItemId = 111,
                        ItemName = "Thuốc hạ sốt Paracetamol 500mg",
                        Unit = "viên",
                        AvailableQuantity = 500
                    }
                ]
            }
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "ReconcileSupplyShortagesWithInventory", result.SupplyShortages, depots, result.SuggestedActivities);
        InvokeStatic(nameof(RescueMissionSuggestionService), "NormalizeSupplyShortages", result);

        Assert.Empty(result.SupplyShortages);
        Assert.False(result.NeedsAdditionalDepot);
        Assert.True(string.IsNullOrWhiteSpace(result.SpecialNotes));
    }

    [Fact]
    public void ReconcileSupplyShortagesWithInventory_RenamesGenericBlanketShortageToActualInventoryItem()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto
                {
                    Step = 1,
                    ActivityType = "COLLECT_SUPPLIES",
                    DepotId = 3,
                    DepotName = "Kho Huế"
                }
            ],
            SupplyShortages =
            [
                new SupplyShortageDto
                {
                    SosRequestId = 4,
                    ItemName = "Chăn màn",
                    Unit = "cái",
                    NeededQuantity = 6,
                    AvailableQuantity = 0,
                    MissingQuantity = 6
                }
            ]
        };

        var depots = new List<DepotSummary>
        {
            new()
            {
                Id = 3,
                Name = "Kho Huế",
                Inventories =
                [
                    new DepotInventoryItemDto
                    {
                        ItemId = 6,
                        ItemName = "Chăn ấm giữ nhiệt",
                        Unit = "cái",
                        AvailableQuantity = 2
                    }
                ]
            }
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "ReconcileSupplyShortagesWithInventory", result.SupplyShortages, depots, result.SuggestedActivities);
        InvokeStatic(nameof(RescueMissionSuggestionService), "NormalizeSupplyShortages", result);

        var shortage = Assert.Single(result.SupplyShortages);
        Assert.Equal("Chăn ấm giữ nhiệt", shortage.ItemName);
        Assert.Equal(2, shortage.AvailableQuantity);
        Assert.Equal(4, shortage.MissingQuantity);
        Assert.Contains("Chăn ấm giữ nhiệt", result.SpecialNotes);
        Assert.DoesNotContain("Chăn màn", result.SpecialNotes);
    }

    [Fact]
    public void ApplySingleDepotConstraint_FlagsManualReviewWhenMultipleDepotsAppear()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto { Step = 1, ActivityType = "COLLECT_SUPPLIES", DepotId = 1, DepotName = "Kho A" },
                new SuggestedActivityDto { Step = 2, ActivityType = "COLLECT_SUPPLIES", DepotId = 2, DepotName = "Kho B" }
            ]
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "ApplySingleDepotConstraint", result);

        Assert.True(result.NeedsManualReview);
        Assert.Contains("Kho A", result.SpecialNotes);
        Assert.Contains("Kho B", result.SpecialNotes);
    }

    [Fact]
    public void NormalizeEstimatedDurations_FormatsActivitiesAndRecomputesMissionTotal()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto { Step = 2, ActivityType = "RESCUE", EstimatedTime = "1 giờ" },
                new SuggestedActivityDto { Step = 1, ActivityType = "COLLECT_SUPPLIES", EstimatedTime = "65 phút" }
            ],
            EstimatedDuration = "125 phút"
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "NormalizeEstimatedDurations", result);

        Assert.Equal("1 giờ 5 phút", result.SuggestedActivities.Single(activity => activity.Step == 1).EstimatedTime);
        Assert.Equal("1 giờ", result.SuggestedActivities.Single(activity => activity.Step == 2).EstimatedTime);
        Assert.Equal("2 giờ 5 phút", result.EstimatedDuration);
        Assert.False(result.NeedsManualReview);
    }

    [Fact]
    public void ApplyMixedRescueReliefSafetyNote_FlagsManualReviewAndListsSosGroups()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto { Step = 1, ActivityType = "RESCUE", SosRequestId = 11 },
                new SuggestedActivityDto { Step = 2, ActivityType = "DELIVER_SUPPLIES", SosRequestId = 22 }
            ]
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "ApplyMixedRescueReliefSafetyNote", result);

        Assert.True(result.NeedsManualReview);
        Assert.Contains("SOS #11", result.MixedRescueReliefWarning);
        Assert.Contains("SOS #22", result.MixedRescueReliefWarning);
        Assert.DoesNotContain("Safe Zone", result.MixedRescueReliefWarning, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Assembly Point", result.MixedRescueReliefWarning, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mission", result.MixedRescueReliefWarning, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coordinator", result.MixedRescueReliefWarning, StringComparison.OrdinalIgnoreCase);
        Assert.True(string.IsNullOrWhiteSpace(result.SpecialNotes));
    }

    [Fact]
    public void BuildMixedRescueReliefWarning_KeepsSameSosIdInBothBranches()
    {
        var warning = MissionSuggestionWarningHelper.BuildMixedRescueReliefWarning(
        [
            new SuggestedActivityDto { Step = 1, ActivityType = "MEDICAL_AID", SosRequestId = 15 },
            new SuggestedActivityDto { Step = 2, ActivityType = "DELIVER_SUPPLIES", SosRequestId = 15 }
        ]);

        Assert.Contains("nhi\u1EC7m v\u1EE5 c\u1EE9u h\u1ED9/c\u1EA5p c\u1EE9u cho SOS #15", warning);
        Assert.Contains("nhi\u1EC7m v\u1EE5 c\u1EE9u tr\u1EE3/c\u1EA5p ph\u00E1t cho SOS #15", warning);
    }

    [Fact]
    public void HydrateReturnSuppliesFromCollectSnapshots_CopiesReusableUnitsFromPlannedPickup()
    {
        var activities = new List<SuggestedActivityDto>
        {
            new()
            {
                Step = 1,
                ActivityType = "COLLECT_SUPPLIES",
                DepotId = 1,
                SuggestedTeam = new SuggestedTeamDto { TeamId = 21, TeamName = "Team A" },
                SuppliesToCollect =
                [
                    new SupplyToCollectDto
                    {
                        ItemId = 105,
                        ItemName = "Ca no cuu ho",
                        Quantity = 2,
                        Unit = "chiec",
                        PlannedPickupReusableUnits =
                        [
                            new SupplyExecutionReusableUnitDto { ReusableItemId = 501, ItemModelId = 105, ItemName = "Ca no cuu ho", SerialNumber = "CN-001", Condition = "Good" },
                            new SupplyExecutionReusableUnitDto { ReusableItemId = 502, ItemModelId = 105, ItemName = "Ca no cuu ho", SerialNumber = "CN-002", Condition = "Fair" }
                        ]
                    }
                ]
            },
            new()
            {
                Step = 2,
                ActivityType = "RETURN_SUPPLIES",
                DepotId = 1,
                SuggestedTeam = new SuggestedTeamDto { TeamId = 21, TeamName = "Team A" },
                SuppliesToCollect =
                [
                    new SupplyToCollectDto
                    {
                        ItemId = 105,
                        ItemName = "Ca no cuu ho",
                        Quantity = 2,
                        Unit = "chiec"
                    }
                ]
            }
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "HydrateReturnSuppliesFromCollectSnapshots", activities);

        var returnSupply = Assert.Single(activities[1].SuppliesToCollect!);
        Assert.NotNull(returnSupply.ExpectedReturnUnits);
        Assert.Equal(["CN-001", "CN-002"], returnSupply.ExpectedReturnUnits!.Select(unit => unit.SerialNumber ?? string.Empty).ToArray());
    }

    [Fact]
    public void HydrateReturnSuppliesFromCollectSnapshots_CopiesOnlyReturnedConsumableQuantityByLot()
    {
        var receivedDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        var activities = new List<SuggestedActivityDto>
        {
            new()
            {
                Step = 1,
                ActivityType = "COLLECT_SUPPLIES",
                DepotId = 1,
                SuggestedTeam = new SuggestedTeamDto { TeamId = 21, TeamName = "Team A" },
                SuppliesToCollect =
                [
                    new SupplyToCollectDto
                    {
                        ItemId = 15,
                        ItemName = "Nuoc khoang",
                        Quantity = 9,
                        Unit = "chai",
                        PlannedPickupLotAllocations =
                        [
                            new SupplyExecutionLotDto { LotId = 7001, QuantityTaken = 5, ReceivedDate = receivedDate, RemainingQuantityAfterExecution = 95 },
                            new SupplyExecutionLotDto { LotId = 7002, QuantityTaken = 4, ReceivedDate = receivedDate.AddDays(1), RemainingQuantityAfterExecution = 96 }
                        ]
                    }
                ]
            },
            new()
            {
                Step = 2,
                ActivityType = "RETURN_SUPPLIES",
                DepotId = 1,
                SuggestedTeam = new SuggestedTeamDto { TeamId = 21, TeamName = "Team A" },
                SuppliesToCollect =
                [
                    new SupplyToCollectDto
                    {
                        ItemId = 15,
                        ItemName = "Nuoc khoang",
                        Quantity = 6,
                        Unit = "chai"
                    }
                ]
            }
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "HydrateReturnSuppliesFromCollectSnapshots", activities);

        var returnSupply = Assert.Single(activities[1].SuppliesToCollect!);
        Assert.NotNull(returnSupply.ExpectedReturnLotAllocations);
        Assert.Collection(
            returnSupply.ExpectedReturnLotAllocations!,
            lot =>
            {
                Assert.Equal(7001, lot.LotId);
                Assert.Equal(5, lot.QuantityTaken);
            },
            lot =>
            {
                Assert.Equal(7002, lot.LotId);
                Assert.Equal(1, lot.QuantityTaken);
            });
    }

    private static RescueMissionSuggestionResult ParseMissionSuggestion(string response)
    {
        var method = typeof(RescueMissionSuggestionService).GetMethod(
            "ParseMissionSuggestion",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return (RescueMissionSuggestionResult)method!.Invoke(null, [response])!;
    }

    private static T DeserializePipelineFragment<T>(string rawResponse)
    {
        var method = typeof(RescueMissionSuggestionService)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "DeserializePipelineFragment" && m.IsGenericMethodDefinition);

        Assert.NotNull(method);
        var generic = method!.MakeGenericMethod(typeof(T));
        return (T)generic.Invoke(null, [rawResponse])!;
    }

    private static void InvokeStatic(string typeName, string methodName, params object?[] args)
    {
        _ = typeName;
        var method = typeof(RescueMissionSuggestionService).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        method!.Invoke(null, args);
    }

    private static T? InvokeStaticResult<T>(string typeName, string methodName, params object?[] args)
    {
        _ = typeName;
        var method = typeof(RescueMissionSuggestionService).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return (T?)method!.Invoke(null, args);
    }
}
