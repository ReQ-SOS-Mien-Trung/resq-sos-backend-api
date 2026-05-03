using System.Reflection;
using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Domain.Enum.System;
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
              ]
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
              "warning_reason": "Cluster co nhieu diem nguy co dang xem xet."
            }
            """);

        Assert.True(result.NeedsManualReview);
        Assert.Contains("Can xem xet bo sung", result.SpecialNotes);
        Assert.Contains("#11", result.SpecialNotes);
        Assert.Contains("#12", result.SpecialNotes);
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
              "warning_reason": "Can uu tien safe transfer truoc khi tiep te."
            }
            """);

        Assert.True(result.NeedsManualReview);
        Assert.Contains("Mixed route khong an toan", result.MixedRescueReliefWarning);
        Assert.Contains("#11", result.MixedRescueReliefWarning);
        Assert.Contains("#22", result.MixedRescueReliefWarning);
    }

    [Fact]
    public void ParseMissionSuggestion_ParsesActivityTargetPersonIds()
    {
        var result = ParseMissionSuggestion(
            """
            {
              "mission_title": "Medical mission",
              "activities": [
                {
                  "step": 1,
                  "activity_type": "MEDICAL_AID",
                  "description": "Cham soc y te cho ong Khoa",
                  "sos_request_id": 28,
                  "target_person_ids": ["adult_1"],
                  "target_victim_summary": "ong Khoa"
                }
              ]
            }
            """);

        var activity = Assert.Single(result.SuggestedActivities);
        Assert.Equal(["adult_1"], activity.TargetPersonIds);
        Assert.Equal("ong Khoa", activity.TargetVictimSummary);
    }

    [Fact]
    public void EnrichVictimTargets_MedicalAidUsesAiTargetPersonIds()
    {
        var activities = new List<SuggestedActivityDto>
        {
            new()
            {
                ActivityType = "MEDICAL_AID",
                Description = "Cham soc y te cho ong Khoa",
                SosRequestId = 28,
                TargetPersonIds = ["adult_1"],
                TargetVictimSummary = "ong Khoa"
            }
        };
        var sosLookup = BuildVictimTargetSosLookup();

        InvokeStatic(nameof(RescueMissionSuggestionService), "EnrichVictimTargets", activities, sosLookup);

        var activity = Assert.Single(activities);
        var victim = Assert.Single(activity.TargetVictims);
        Assert.Equal("adult_1", victim.PersonId);
        Assert.Equal("ong Khoa", activity.TargetVictimSummary);
        Assert.DoesNotContain(activity.TargetVictims, item => item.PersonId == "child_1");
    }

    [Fact]
    public void EnrichVictimTargets_MedicalAidWithoutAiTarget_DoesNotFallbackToAllVictims()
    {
        var activities = new List<SuggestedActivityDto>
        {
            new()
            {
                ActivityType = "MEDICAL_AID",
                Description = "Cham soc y te tai hien truong",
                SosRequestId = 28
            }
        };
        var sosLookup = BuildVictimTargetSosLookup();

        InvokeStatic(nameof(RescueMissionSuggestionService), "EnrichVictimTargets", activities, sosLookup);

        var activity = Assert.Single(activities);
        Assert.Empty(activity.TargetVictims);
        Assert.Null(activity.TargetVictimSummary);
    }

    [Fact]
    public void EnrichVictimTargets_RescueWithoutAiTarget_KeepsExistingVictimFallback()
    {
        var activities = new List<SuggestedActivityDto>
        {
            new()
            {
                ActivityType = "RESCUE",
                Description = "Tiep can hien truong",
                SosRequestId = 28
            }
        };
        var sosLookup = BuildVictimTargetSosLookup();

        InvokeStatic(nameof(RescueMissionSuggestionService), "EnrichVictimTargets", activities, sosLookup);

        var activity = Assert.Single(activities);
        Assert.Equal(2, activity.TargetVictims.Count);
        Assert.Contains(activity.TargetVictims, victim => victim.PersonId == "adult_1");
        Assert.Contains(activity.TargetVictims, victim => victim.PersonId == "child_1");
    }

    [Fact]
    public void EnrichVictimTargets_RescueWithEmptyAiTarget_KeepsExistingVictimFallback()
    {
        var activities = new List<SuggestedActivityDto>
        {
            new()
            {
                ActivityType = "RESCUE",
                Description = "Tiep can hien truong",
                SosRequestId = 28,
                TargetPersonIds = []
            }
        };
        var sosLookup = BuildVictimTargetSosLookup();

        InvokeStatic(nameof(RescueMissionSuggestionService), "EnrichVictimTargets", activities, sosLookup);

        var activity = Assert.Single(activities);
        Assert.Equal(2, activity.TargetVictims.Count);
        Assert.Contains(activity.TargetVictims, victim => victim.PersonId == "adult_1");
        Assert.Contains(activity.TargetVictims, victim => victim.PersonId == "child_1");
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
    public void DeserializePipelineFragment_Requirements_CoercesLooseAiScalars()
    {
        var fragment = DeserializePipelineFragment<MissionRequirementsFragment>(
            """
            {
              "suggested_mission_title": "Rescue for fracture and hypothermia",
              "suggested_mission_type": "RESCUE",
              "suggested_priority_score": "8.5/10",
              "suggested_severity_level": "Severe",
              "needs_additional_depot": "false",
              "split_cluster_recommended": "false",
              "suggested_resources": [
                {
                  "resource_type": { "value": "EQUIPMENT" },
                  "description": ["stretcher", "thermal blanket"],
                  "quantity": "2 items",
                  "priority": "High"
                }
              ],
              "sos_requirements": [
                {
                  "sos_request_id": "SOS #191",
                  "summary": { "text": "Adult victim has fracture and hypothermia risk" },
                  "priority": "Critical",
                  "needs_immediate_safe_transfer": "true",
                  "can_wait_for_combined_mission": "false",
                  "required_supplies": [
                    {
                      "item_name": { "text": "thermal blanket" },
                      "quantity": "1 bo",
                      "unit": { "value": "bo" },
                      "category": "medical",
                      "notes": ["fracture", "hypothermia"]
                    }
                  ],
                  "required_teams": [
                    {
                      "team_type": { "name": "Medical rescue" },
                      "quantity": "one",
                      "reason": ["fracture first aid"]
                    }
                  ]
                }
              ]
            }
            """);

        Assert.Equal(8.5, fragment.SuggestedPriorityScore);
        Assert.False(fragment.NeedsAdditionalDepot);
        Assert.False(fragment.SplitClusterRecommended);

        var resource = Assert.Single(fragment.SuggestedResources);
        Assert.Equal("EQUIPMENT", resource.ResourceType);
        Assert.Equal(2, resource.Quantity);

        var requirement = Assert.Single(fragment.SosRequirements);
        Assert.Equal(191, requirement.SosRequestId);
        Assert.True(requirement.NeedsImmediateSafeTransfer);
        Assert.False(requirement.CanWaitForCombinedMission);
        Assert.Equal("Adult victim has fracture and hypothermia risk", requirement.Summary);

        var supply = Assert.Single(requirement.RequiredSupplies);
        Assert.Equal("thermal blanket", supply.ItemName);
        Assert.Equal(1, supply.Quantity);
        Assert.Equal("bo", supply.Unit);
        Assert.Equal("fracture, hypothermia", supply.Notes);

        var team = Assert.Single(requirement.RequiredTeams);
        Assert.Equal("Medical rescue", team.TeamType);
        Assert.Equal(1, team.Quantity);
        Assert.Equal("fracture first aid", team.Reason);
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
              }
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
              "suggested_team": []
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
    public void ApplySingleSelectedDepotToSupplyActivities_FillsMissingDepotOnDeliverActivity()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto
                {
                    Step = 1,
                    ActivityType = "COLLECT_SUPPLIES",
                    DepotId = 1,
                    DepotName = "Kho A",
                    DepotAddress = "1 Le Loi",
                    SuppliesToCollect =
                    [
                        new SupplyToCollectDto
                        {
                            ItemId = 15,
                            ItemName = "Nuoc sach",
                            Quantity = 10,
                            Unit = "chai"
                        }
                    ]
                },
                new SuggestedActivityDto
                {
                    Step = 2,
                    ActivityType = "DELIVER_SUPPLIES",
                    SosRequestId = 22,
                    SuppliesToCollect =
                    [
                        new SupplyToCollectDto
                        {
                            ItemId = 15,
                            ItemName = "Nuoc sach",
                            Quantity = 10,
                            Unit = "chai"
                        }
                    ]
                }
            ]
        };

        InvokeStatic(
            nameof(RescueMissionSuggestionService),
            "ApplySingleSelectedDepotToSupplyActivities",
            result,
            new List<DepotSummary>
            {
                new()
                {
                    Id = 1,
                    Name = "Kho A",
                    Address = "1 Le Loi"
                }
            });

        Assert.Equal(1, result.SuggestedActivities[1].DepotId);
        Assert.Equal("Kho A", result.SuggestedActivities[1].DepotName);
        Assert.Equal("1 Le Loi", result.SuggestedActivities[1].DepotAddress);
    }

    [Fact]
    public void AugmentRequirementsFromStructuredData_AddsGroupBlanketAndElderlyClothes()
    {
        var requirements = new MissionRequirementsFragment
        {
            SosRequirements =
            [
                new MissionSosRequirementFragment
                {
                    SosRequestId = 23,
                    RequiredSupplies = [],
                    RequiredTeams = []
                }
            ]
        };
        var sosRequests = new List<SosRequestSummary>
        {
            new()
            {
                Id = 23,
                StructuredData =
                    """
                    {
                      "incident": {
                        "people_count": { "adult": 0, "child": 0, "elderly": 1 },
                        "has_injured": true,
                        "need_medical": true
                      },
                      "group_needs": {
                        "supplies": ["WATER", "FOOD", "CLOTHES", "BLANKET", "MEDICINE", "OTHER"],
                        "water": { "duration": "6_TO_12H" },
                        "food": { "duration": "12_TO_24H" },
                        "blanket": {
                          "availability": "NOT_ENOUGH",
                          "request_count": null
                        },
                        "medicine": {
                          "needs_urgent_medicine": true,
                          "medical_needs": ["COMMON_MEDICINE", "FIRST_AID"]
                        },
                        "clothing": {
                          "status": "PARTIALLY_LACKING",
                          "needed_people_count": null
                        },
                        "other_supply_description": "Pin sạc dự phòng"
                      },
                      "victims": [
                        {
                          "person_id": "elderly_1",
                          "person_type": "ELDERLY",
                          "personal_needs": {
                            "clothing": { "needed": false }
                          }
                        }
                      ]
                    }
                    """
            }
        };

        InvokeStatic(
            nameof(RescueMissionSuggestionService),
            "AugmentRequirementsFromStructuredData",
            requirements,
            sosRequests);

        var supplies = Assert.Single(requirements.SosRequirements).RequiredSupplies;
        Assert.Contains(supplies, supply => supply.ItemName == "Chăn ấm giữ nhiệt" && supply.Quantity == 1);
        Assert.Contains(supplies, supply => supply.ItemName == "Bộ quần áo người cao tuổi" && supply.Quantity == 1);
    }

    [Fact]
    public void BackfillItemIds_CanonicalizesGenericSupplyNameToInventoryName()
    {
        var activities = new List<SuggestedActivityDto>
        {
            new()
            {
                Step = 1,
                ActivityType = "COLLECT_SUPPLIES",
                DepotId = 1,
                SuppliesToCollect =
                [
                    new SupplyToCollectDto
                    {
                        ItemName = "Nuoc",
                        Quantity = 10
                    }
                ]
            }
        };

        var depots = new List<DepotSummary>
        {
            new()
            {
                Id = 1,
                Name = "Kho A",
                Inventories =
                [
                    new DepotInventoryItemDto
                    {
                        ItemId = 15,
                        ItemName = "Nuoc khoang Lavie 500ml",
                        Unit = "chai",
                        AvailableQuantity = 100
                    }
                ]
            }
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "BackfillItemIds", activities, depots);

        var supply = Assert.Single(activities[0].SuppliesToCollect!);
        Assert.Equal(15, supply.ItemId);
        Assert.Equal("Nuoc khoang Lavie 500ml", supply.ItemName);
        Assert.Equal("chai", supply.Unit);
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
    public void ApplySosCoverageReview_FlagsManualReviewWhenClusterSosIsMissingDirectActivity()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto { Step = 1, ActivityType = "DELIVER_SUPPLIES", SosRequestId = 1 }
            ]
        };

        InvokeStatic(
            nameof(RescueMissionSuggestionService),
            "ApplySosCoverageReview",
            result,
            new List<SosRequestSummary>
            {
                new() { Id = 1 },
                new() { Id = 2 }
            });

        Assert.True(result.NeedsManualReview);
        Assert.Contains("SOS #2", result.SpecialNotes);
        Assert.Contains("DELIVER_SUPPLIES/RESCUE/MEDICAL_AID/EVACUATE", result.SpecialNotes);
    }

    [Fact]
    public void ApplySosCoverageReview_DoesNotWarnWhenEverySosHasDirectCoverageActivity()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto { Step = 1, ActivityType = "DELIVER_SUPPLIES", SosRequestId = 1 },
                new SuggestedActivityDto { Step = 2, ActivityType = "RESCUE", SosRequestId = 2 }
            ]
        };

        InvokeStatic(
            nameof(RescueMissionSuggestionService),
            "ApplySosCoverageReview",
            result,
            new List<SosRequestSummary>
            {
                new() { Id = 1 },
                new() { Id = 2 }
            });

        Assert.False(result.NeedsManualReview);
        Assert.True(string.IsNullOrWhiteSpace(result.SpecialNotes));
    }

    [Fact]
    public void ApplySosCoverageReview_IgnoresCollectReturnAndDescriptionOnlySosMentions()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto
                {
                    Step = 1,
                    ActivityType = "DELIVER_SUPPLIES",
                    SosRequestId = 1,
                    Description = "Giao vat tu cho SOS ID 1 va SOS ID 2"
                },
                new SuggestedActivityDto { Step = 2, ActivityType = "COLLECT_SUPPLIES", SosRequestId = 2 },
                new SuggestedActivityDto { Step = 3, ActivityType = "RETURN_SUPPLIES", SosRequestId = 2 },
                new SuggestedActivityDto { Step = 4, ActivityType = "RETURN_ASSEMBLY_POINT", SosRequestId = 2 }
            ]
        };

        InvokeStatic(
            nameof(RescueMissionSuggestionService),
            "ApplySosCoverageReview",
            result,
            new List<SosRequestSummary>
            {
                new() { Id = 1 },
                new() { Id = 2 }
            });

        Assert.True(result.NeedsManualReview);
        Assert.Contains("SOS #2", result.SpecialNotes);
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
    public void ApplyMixedRescueReliefSafetyNote_IgnoresSingleSharedSos()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto { Step = 1, ActivityType = "MEDICAL_AID", SosRequestId = 15 },
                new SuggestedActivityDto { Step = 2, ActivityType = "DELIVER_SUPPLIES", SosRequestId = 15 }
            ]
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "ApplyMixedRescueReliefSafetyNote", result);

        Assert.False(result.NeedsManualReview);
        Assert.True(string.IsNullOrWhiteSpace(result.MixedRescueReliefWarning));
    }

    [Fact]
    public void BuildMixedRescueReliefWarning_IgnoresSameSosIdInBothBranches()
    {
        var warning = MissionSuggestionWarningHelper.BuildMixedRescueReliefWarning(
        [
            new SuggestedActivityDto { Step = 1, ActivityType = "MEDICAL_AID", SosRequestId = 15 },
            new SuggestedActivityDto { Step = 2, ActivityType = "DELIVER_SUPPLIES", SosRequestId = 15 }
        ]);

        Assert.True(string.IsNullOrWhiteSpace(warning));
    }

    [Fact]
    public void BuildMixedRescueReliefWarning_IgnoresCollectOnlyRescueGear()
    {
        var warning = MissionSuggestionWarningHelper.BuildMixedRescueReliefWarning(
        [
            new SuggestedActivityDto { Step = 1, ActivityType = "COLLECT_SUPPLIES", SosRequestId = 15 },
            new SuggestedActivityDto { Step = 2, ActivityType = "RESCUE", SosRequestId = 15 }
        ]);

        Assert.True(string.IsNullOrWhiteSpace(warning));
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

    [Theory]
    [InlineData(PromptType.MissionRequirementsAssessment, "MEDICAL_ITEM_SELECTION (STRICT)")]
    [InlineData(PromptType.MissionRequirementsAssessment, "Bo so cuu co ban")]
    [InlineData(PromptType.MissionRequirementsAssessment, "REALISTIC_ESTIMATE_TIME (STRICT)")]
    [InlineData(PromptType.MissionDepotPlanning, "DELIVER_ONLY_CONSUMABLES_IN_COLLECT (STRICT)")]
    [InlineData(PromptType.MissionDepotPlanning, "Do NOT list Consumable items in COLLECT_SUPPLIES")]
    [InlineData(PromptType.MissionDepotPlanning, "Backend will automatically sum up DELIVER_SUPPLIES items")]
    [InlineData(PromptType.MissionDepotPlanning, "REUSABLE_FIELD_USE_BEFORE_RETURN (STRICT)")]
    [InlineData(PromptType.MissionDepotPlanning, "MEDICAL_ITEM_SELECTION (STRICT)")]
    [InlineData(PromptType.MissionDepotPlanning, "REALISTIC_ESTIMATE_TIME (STRICT)")]
    [InlineData(PromptType.MissionDepotPlanning, "5-15 minutes for collect/deliver/return")]
    [InlineData(PromptType.MissionDepotPlanning, "15-35 minutes for rescue/medical/evacuate")]
    [InlineData(PromptType.MissionTeamPlanning, "REUSABLE_FIELD_USE_BEFORE_RETURN (STRICT)")]
    [InlineData(PromptType.MissionTeamPlanning, "RETURN_SUPPLIES is only valid after")]
    [InlineData(PromptType.MissionTeamPlanning, "REALISTIC_ESTIMATE_TIME (STRICT)")]
    [InlineData(PromptType.MissionTeamPlanning, "15-35 minutes for rescue/medical/evacuate")]
    [InlineData(PromptType.MissionTeamPlanning, "TARGET_VICTIM_SELECTION_FOR_MEDICAL_AID (STRICT)")]
    [InlineData(PromptType.MissionTeamPlanning, "target_person_ids")]
    [InlineData(PromptType.MissionPlanValidation, "DELIVER_ONLY_CONSUMABLES_IN_COLLECT (STRICT)")]
    [InlineData(PromptType.MissionPlanValidation, "Do NOT add Consumable items to COLLECT")]
    [InlineData(PromptType.MissionPlanValidation, "REUSABLE_FIELD_USE_BEFORE_RETURN (STRICT)")]
    [InlineData(PromptType.MissionPlanValidation, "explicitly mention using the collected Reusable equipment by name")]
    [InlineData(PromptType.MissionPlanValidation, "MEDICAL_ITEM_SELECTION (STRICT)")]
    [InlineData(PromptType.MissionPlanValidation, "REALISTIC_ESTIMATE_TIME (STRICT)")]
    [InlineData(PromptType.MissionPlanValidation, "TARGET_VICTIM_SELECTION_FOR_MEDICAL_AID (STRICT)")]
    [InlineData(PromptType.MissionPlanValidation, "target_person_ids")]
    public void BuildPipelineStageAppendix_ContainsExpectedGuardrailRule(PromptType promptType, string expectedRule)
    {
        var appendix = BuildStageAppendix(promptType, string.Empty);

        Assert.Contains(expectedRule, appendix);
    }

    [Fact]
    public void BackfillCollectSuppliesFromDeliveries_AggregatesDeliverItemsIntoCollect()
    {
        var activities = new List<SuggestedActivityDto>
        {
            new()
            {
                ActivityType = "COLLECT_SUPPLIES",
                DepotId = 1,
                SuppliesToCollect = null
            },
            new()
            {
                ActivityType = "DELIVER_SUPPLIES",
                DepotId = 1,
                SosRequestId = 10,
                SuppliesToCollect =
                [
                    new SupplyToCollectDto { ItemId = 5, ItemName = "Lương khô", Quantity = 2, Unit = "gói" }
                ]
            },
            new()
            {
                ActivityType = "DELIVER_SUPPLIES",
                DepotId = 1,
                SosRequestId = 11,
                SuppliesToCollect =
                [
                    new SupplyToCollectDto { ItemId = 5, ItemName = "Lương khô", Quantity = 2, Unit = "gói" },
                    new SupplyToCollectDto { ItemId = 7, ItemName = "Nước uống", Quantity = 6, Unit = "chai" }
                ]
            }
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "BackfillCollectSuppliesFromDeliveries", activities);

        var collect = activities.Single(a => a.ActivityType == "COLLECT_SUPPLIES");
        Assert.NotNull(collect.SuppliesToCollect);
        var luongKho = collect.SuppliesToCollect!.Single(s => s.ItemId == 5);
        Assert.Equal(4, luongKho.Quantity);
        var nuoc = collect.SuppliesToCollect!.Single(s => s.ItemId == 7);
        Assert.Equal(6, nuoc.Quantity);
    }

    [Fact]
    public void BackfillCollectSuppliesFromDeliveries_PreservesReusableItemsAlreadyInCollect()
    {
        var activities = new List<SuggestedActivityDto>
        {
            new()
            {
                ActivityType = "COLLECT_SUPPLIES",
                DepotId = 1,
                SuppliesToCollect =
                [
                    new SupplyToCollectDto { ItemId = 99, ItemName = "Xe tải cứu trợ", Quantity = 1, Unit = "xe" }
                ]
            },
            new()
            {
                ActivityType = "DELIVER_SUPPLIES",
                DepotId = 1,
                SosRequestId = 10,
                SuppliesToCollect =
                [
                    new SupplyToCollectDto { ItemId = 5, ItemName = "Lương khô", Quantity = 3, Unit = "gói" }
                ]
            }
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "BackfillCollectSuppliesFromDeliveries", activities);

        var collect = activities.Single(a => a.ActivityType == "COLLECT_SUPPLIES");
        Assert.NotNull(collect.SuppliesToCollect);
        Assert.Equal(2, collect.SuppliesToCollect!.Count);
        Assert.Contains(collect.SuppliesToCollect, s => s.ItemId == 99);
        Assert.Contains(collect.SuppliesToCollect, s => s.ItemId == 5 && s.Quantity == 3);
    }

    private static string BuildStageAppendix(PromptType promptType, string extraAppendix)
    {
        var method = typeof(RescueMissionSuggestionService).GetMethod(
            "BuildPipelineStageAppendix",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return (string)method!.Invoke(null, [promptType, extraAppendix])!;
    }

    private static Dictionary<int, SosRequestSummary> BuildVictimTargetSosLookup() =>
        new()
        {
            [28] = new SosRequestSummary
            {
                Id = 28,
                TargetVictimSummary = "Nguoi lon 1 (adult), Tre em 1 (child)",
                TargetVictims =
                [
                    new MissionActivityTargetVictimDto
                    {
                        PersonId = "adult_1",
                        DisplayName = "Nguoi lon 1",
                        PersonType = "ADULT",
                        Index = 1
                    },
                    new MissionActivityTargetVictimDto
                    {
                        PersonId = "child_1",
                        DisplayName = "Tre em 1",
                        PersonType = "CHILD",
                        Index = 1
                    }
                ]
            }
        };

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
