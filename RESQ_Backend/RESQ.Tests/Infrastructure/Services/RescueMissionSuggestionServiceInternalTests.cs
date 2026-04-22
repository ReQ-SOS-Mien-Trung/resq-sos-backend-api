using System.Reflection;
using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Emergency.Shared;
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
    public void BackfillItemIds_MapsAliasBasedSupplyLabelsToInventoryItems()
    {
        var activities = new List<SuggestedActivityDto>
        {
            new()
            {
                Step = 1,
                ActivityType = "COLLECT_SUPPLIES",
                DepotId = 9,
                DepotName = "Kho A",
                SuppliesToCollect =
                [
                    new SupplyToCollectDto
                    {
                        ItemName = "Nuoc",
                        Quantity = 10,
                        Unit = "chai"
                    },
                    new SupplyToCollectDto
                    {
                        ItemName = "Sua",
                        Quantity = 2,
                        Unit = "hop"
                    }
                ]
            }
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
                        ItemId = 41,
                        ItemName = "Nuoc khoang dong chai",
                        Unit = "chai",
                        AvailableQuantity = 50
                    },
                    new DepotInventoryItemDto
                    {
                        ItemId = 42,
                        ItemName = "Sua dinh duong tre em",
                        Unit = "hop",
                        AvailableQuantity = 12
                    }
                ]
            }
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "BackfillItemIds", activities, depots);

        var supplies = activities[0].SuppliesToCollect!;
        Assert.Equal(41, supplies[0].ItemId);
        Assert.Equal("Nuoc khoang dong chai", supplies[0].ItemName);
        Assert.Equal(42, supplies[1].ItemId);
        Assert.Equal("Sua dinh duong tre em", supplies[1].ItemName);
    }

    [Fact]
    public void ConvertUnresolvedSuppliesToShortages_RemovesFakeSupplyFromActivities()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto
                {
                    Step = 1,
                    ActivityType = "COLLECT_SUPPLIES",
                    SosRequestId = 7,
                    DepotId = 9,
                    DepotName = "Kho A",
                    SuppliesToCollect =
                    [
                        new SupplyToCollectDto
                        {
                            ItemName = "Nhu yeu pham thiet yeu",
                            Quantity = 1,
                            Unit = "goi"
                        },
                        new SupplyToCollectDto
                        {
                            ItemId = 50,
                            ItemName = "Nuoc khoang",
                            Quantity = 4,
                            Unit = "chai"
                        }
                    ]
                },
                new SuggestedActivityDto
                {
                    Step = 2,
                    ActivityType = "DELIVER_SUPPLIES",
                    SosRequestId = 7,
                    DepotId = 9,
                    DepotName = "Kho A",
                    SuppliesToCollect =
                    [
                        new SupplyToCollectDto
                        {
                            ItemName = "Nhu yeu pham thiet yeu",
                            Quantity = 1,
                            Unit = "goi"
                        }
                    ]
                }
            ]
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "ConvertUnresolvedSuppliesToShortages", result);

        Assert.True(result.NeedsManualReview);
        var collectSupplies = Assert.Single(result.SuggestedActivities[0].SuppliesToCollect!);
        Assert.Equal(50, collectSupplies.ItemId);
        Assert.Null(result.SuggestedActivities[1].SuppliesToCollect);
        var shortage = Assert.Single(result.SupplyShortages);
        Assert.Equal("Nhu yeu pham thiet yeu", shortage.ItemName);
        Assert.Contains("supply_shortages", result.SpecialNotes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyNearbyTeamConstraints_NormalizesActivitiesToSingleTeam()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto
                {
                    Step = 1,
                    ActivityType = "RESCUE",
                    ExecutionMode = "SplitAcrossTeams",
                    RequiredTeamCount = 3,
                    CoordinationGroupKey = "urgent-cluster",
                    CoordinationNotes = "Batch SOS gan nhau"
                }
            ]
        };

        RescueMissionSuggestionReviewHelper.ApplyNearbyTeamConstraints(
            result,
            [
                new AgentTeamInfo
                {
                    TeamId = 7,
                    TeamName = "Team 7",
                    TeamType = "Rescue",
                    IsAvailable = true
                }
            ]);

        var activity = Assert.Single(result.SuggestedActivities);
        Assert.Equal("SingleTeam", activity.ExecutionMode);
        Assert.Equal(1, activity.RequiredTeamCount);
        Assert.Equal("urgent-cluster", activity.CoordinationGroupKey);
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
        var sosLookup = new Dictionary<int, SosRequestSummary>
        {
            [11] = new()
            {
                Id = 11,
                PriorityLevel = "Critical",
                AiAnalysis = new SosRequestAiAnalysisSummary
                {
                    HasAiAnalysis = true,
                    SuggestedPriority = "Critical",
                    NeedsImmediateSafeTransfer = true,
                    CanWaitForCombinedMission = false,
                    HandlingReason = "Immediate evacuation required."
                }
            },
            [22] = new()
            {
                Id = 22,
                PriorityLevel = "Medium",
                AiAnalysis = SosRequestAiAnalysisHelper.CreateFallback("Medium")
            }
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "ApplyMixedRescueReliefSafetyNote", result, sosLookup);

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
    public void BuildMixedRescueReliefWarning_ReturnsEmptyWhenRescueCanWait()
    {
        var warning = MissionSuggestionWarningHelper.BuildMixedRescueReliefWarning(
            [
                new SuggestedActivityDto { Step = 1, ActivityType = "MEDICAL_AID", SosRequestId = 15 },
                new SuggestedActivityDto { Step = 2, ActivityType = "DELIVER_SUPPLIES", SosRequestId = 15 }
            ],
            new Dictionary<int, SosRequestSummary>
            {
                [15] = new()
                {
                    Id = 15,
                    PriorityLevel = "High",
                    AiAnalysis = new SosRequestAiAnalysisSummary
                    {
                        HasAiAnalysis = true,
                        SuggestedPriority = "High",
                        NeedsImmediateSafeTransfer = false,
                        CanWaitForCombinedMission = true
                    }
                }
            });

        Assert.Equal(string.Empty, warning);
    }

    [Fact]
    public void NormalizeActivitySequence_PreservesPipelineOrderAndResequencesSteps()
    {
        var team = new SuggestedTeamDto { TeamId = 7, TeamName = "Team 7" };
        var activities = new List<SuggestedActivityDto>
        {
            new() { Step = 1, ActivityType = "RESCUE", SosRequestId = 11, SuggestedTeam = team },
            new() { Step = 2, ActivityType = "DELIVER_SUPPLIES", SosRequestId = 22, SuggestedTeam = team },
            new() { Step = 3, ActivityType = "COLLECT_SUPPLIES", SosRequestId = 22, SuggestedTeam = team }
        };

        InvokeStatic(
            nameof(RescueMissionSuggestionService),
            "NormalizeActivitySequence",
            activities,
            new Dictionary<int, SosRequestSummary>
            {
                [11] = new() { Id = 11, PriorityLevel = "High", AiAnalysis = SosRequestAiAnalysisHelper.CreateFallback("High") },
                [22] = new() { Id = 22, PriorityLevel = "Medium", AiAnalysis = SosRequestAiAnalysisHelper.CreateFallback("Medium") }
            });

        Assert.Equal(["RESCUE", "DELIVER_SUPPLIES", "COLLECT_SUPPLIES"], activities.Select(activity => activity.ActivityType).ToArray());
        Assert.Equal([1, 2, 3], activities.Select(activity => activity.Step).ToArray());
    }

    [Fact]
    public void ApplyMixedMissionMissingAiAnalysisManualReview_FlagsReviewWithoutUrgentWarning()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto { Step = 1, ActivityType = "RESCUE", SosRequestId = 11 },
                new SuggestedActivityDto { Step = 2, ActivityType = "DELIVER_SUPPLIES", SosRequestId = 22 }
            ]
        };

        InvokeStatic(
            nameof(RescueMissionSuggestionService),
            "ApplyMixedMissionMissingAiAnalysisManualReview",
            result,
            new Dictionary<int, SosRequestSummary>
            {
                [11] = new() { Id = 11, PriorityLevel = "High", AiAnalysis = SosRequestAiAnalysisHelper.CreateFallback("High") },
                [22] = new() { Id = 22, PriorityLevel = "Medium", AiAnalysis = SosRequestAiAnalysisHelper.CreateFallback("Medium") }
            });

        Assert.True(result.NeedsManualReview);
        Assert.Contains("missing SOS AI analysis", result.SpecialNotes, StringComparison.OrdinalIgnoreCase);
        Assert.True(string.IsNullOrWhiteSpace(result.MixedRescueReliefWarning));
    }

    [Fact]
    public void EnrichVictimTargets_PopulatesTargetVictimsFromStructuredData()
    {
        var activities = new List<SuggestedActivityDto>
        {
            new()
            {
                Step = 1,
                ActivityType = "RESCUE",
                SosRequestId = 9,
                Description = "Tiep can hien truong"
            }
        };
        var sosLookup = new Dictionary<int, SosRequestSummary>
        {
            [9] = new()
            {
                Id = 9,
                StructuredData =
                """
                {
                  "incident": {
                    "people_count": {
                      "adult": 1,
                      "child": 1
                    }
                  },
                  "victims": [
                    {
                      "person_id": "victim-1",
                      "person_type": "CHILD",
                      "custom_name": "Khoa"
                    }
                  ]
                }
                """
            }
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "EnrichVictimTargets", activities, sosLookup);

        var activity = Assert.Single(activities);
        Assert.Contains("Khoa", activity.TargetVictimSummary);
        Assert.Equal(2, activity.TargetVictims.Count);
        Assert.Contains("Khoa", activity.Description);
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

    [Fact]
    public void HydrateDeliverySuppliesFromCollectSnapshots_SplitsCollectedLotsAcrossDeliveriesInRouteOrder()
    {
        var receivedDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        var team = new SuggestedTeamDto { TeamId = 21, TeamName = "Team A" };
        var activities = new List<SuggestedActivityDto>
        {
            new()
            {
                Step = 1,
                ActivityType = "COLLECT_SUPPLIES",
                DepotId = 1,
                SuggestedTeam = team,
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
                ActivityType = "DELIVER_SUPPLIES",
                DepotId = 1,
                SuggestedTeam = team,
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
            },
            new()
            {
                Step = 3,
                ActivityType = "DELIVER_SUPPLIES",
                DepotId = 1,
                SuggestedTeam = team,
                SuppliesToCollect =
                [
                    new SupplyToCollectDto
                    {
                        ItemId = 15,
                        ItemName = "Nuoc khoang",
                        Quantity = 3,
                        Unit = "chai"
                    }
                ]
            }
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "HydrateDeliverySuppliesFromCollectSnapshots", activities);

        var firstDeliveryLots = Assert.Single(activities[1].SuppliesToCollect!).AvailableDeliveryLotAllocations;
        Assert.NotNull(firstDeliveryLots);
        Assert.Collection(
            firstDeliveryLots!,
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

        var secondDeliveryLots = Assert.Single(activities[2].SuppliesToCollect!).AvailableDeliveryLotAllocations;
        Assert.NotNull(secondDeliveryLots);
        Assert.Collection(
            secondDeliveryLots!,
            lot =>
            {
                Assert.Equal(7002, lot.LotId);
                Assert.Equal(3, lot.QuantityTaken);
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

    private static T DeserializePipelineFragment<T>(string response)
    {
        var method = typeof(RescueMissionSuggestionService).GetMethod(
            "DeserializePipelineFragment",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var genericMethod = method!.MakeGenericMethod(typeof(T));
        return (T)genericMethod.Invoke(null, [response])!;
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
}
