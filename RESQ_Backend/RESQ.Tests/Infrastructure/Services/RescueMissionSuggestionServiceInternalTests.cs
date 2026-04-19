using System.Reflection;
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

    private static RescueMissionSuggestionResult ParseMissionSuggestion(string response)
    {
        var method = typeof(RescueMissionSuggestionService).GetMethod(
            "ParseMissionSuggestion",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return (RescueMissionSuggestionResult)method!.Invoke(null, [response])!;
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
