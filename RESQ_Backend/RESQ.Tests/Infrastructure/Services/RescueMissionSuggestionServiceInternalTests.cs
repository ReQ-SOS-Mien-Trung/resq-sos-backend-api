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
    public void ApplyMixedRescueReliefSafetyNote_FlagsManualReviewAndAppendsSafetyGuidance()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto { Step = 1, ActivityType = "RESCUE" },
                new SuggestedActivityDto { Step = 2, ActivityType = "DELIVER_SUPPLIES" }
            ]
        };

        InvokeStatic(nameof(RescueMissionSuggestionService), "ApplyMixedRescueReliefSafetyNote", result);

        Assert.True(result.NeedsManualReview);
        Assert.Contains("Safe Zone/Assembly Point", result.SpecialNotes);
        Assert.Contains("tach thanh mission rieng", result.SpecialNotes);
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
