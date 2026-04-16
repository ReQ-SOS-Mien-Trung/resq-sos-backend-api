using System.Reflection;
using System.Text.Json;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;
using RESQ.Domain.Entities.Emergency;
using RESQ.Application.Services;

namespace RESQ.Tests.Application.UseCases.Operations.Queries;

public class MissionAiSuggestionSectionTests
{
    [Fact]
    public void From_PrefersValidatedActivitiesOverDraft()
    {
        var model = new MissionAiSuggestionModel
        {
            Id = 101,
            SuggestedMissionTitle = "Mission A",
            ModelName = "test-model",
            Activities =
            [
                CreateActivitySuggestion("Draft", "draft-activity"),
                CreateActivitySuggestion("Validated", "validated-activity"),
                CreateActivitySuggestion("Execution", "execution-activity")
            ]
        };

        var section = InvokeFrom(model);

        var activity = Assert.Single(section!.SuggestedActivities);
        Assert.Equal("validated-activity", activity.Description);
    }

    [Fact]
    public void From_UsesDraftActivitiesWhenValidatedIsMissing()
    {
        var model = new MissionAiSuggestionModel
        {
            Id = 102,
            SuggestedMissionTitle = "Mission B",
            ModelName = "test-model",
            Activities =
            [
                CreateActivitySuggestion("Execution", "execution-activity"),
                CreateActivitySuggestion("draft", "draft-activity")
            ]
        };

        var section = InvokeFrom(model);

        var activity = Assert.Single(section!.SuggestedActivities);
        Assert.Equal("draft-activity", activity.Description);
    }

    [Fact]
    public void From_FallsBackToFirstAvailableBlobWhenPhasesAreUnknown()
    {
        var model = new MissionAiSuggestionModel
        {
            Id = 103,
            SuggestedMissionTitle = "Mission C",
            ModelName = "test-model",
            Activities =
            [
                CreateActivitySuggestion("Unknown", "first-activity"),
                CreateActivitySuggestion("Another", "second-activity")
            ]
        };

        var section = InvokeFrom(model);

        var activity = Assert.Single(section!.SuggestedActivities);
        Assert.Equal("first-activity", activity.Description);
    }

    [Fact]
    public void From_MapsSnakeCaseMetadataAndActivities()
    {
        var model = new MissionAiSuggestionModel
        {
            Id = 104,
            SuggestedMissionTitle = "Mission D",
            ModelName = "test-model",
            Metadata = """
                {
                  "overall_assessment": "[SOS ID 1]: need water",
                  "estimated_duration": "1 gio 5 phut",
                  "special_notes": "Coordinator needs backup stock",
                  "needs_manual_review": true,
                  "low_confidence_warning": "Confidence is below review threshold.",
                  "needs_additional_depot": true,
                  "supply_shortages": [
                    {
                      "sos_request_id": 1,
                      "item_id": 2,
                      "item_name": "Nuoc sach",
                      "unit": "chai",
                      "selected_depot_id": 9,
                      "selected_depot_name": "Kho A",
                      "needed_quantity": 20,
                      "available_quantity": 5,
                      "missing_quantity": 15,
                      "notes": "selected depot lacks stock"
                    }
                  ],
                  "suggested_resources": [
                    {
                      "resource_type": "TEAM",
                      "description": "Rescue team",
                      "quantity": 1,
                      "priority": "Critical"
                    }
                  ]
                }
                """,
            Activities =
            [
                CreateSnakeCaseActivitySuggestion("Validated", "snake-activity")
            ]
        };

        var section = InvokeFrom(model);

        Assert.NotNull(section);
        Assert.Equal("[SOS ID 1]: need water", section!.OverallAssessment);
        Assert.Equal("1 gio 5 phut", section.EstimatedDuration);
        Assert.Equal("Coordinator needs backup stock", section.SpecialNotes);
        Assert.True(section.NeedsManualReview);
        Assert.Equal("Confidence is below review threshold.", section.LowConfidenceWarning);
        Assert.True(section.NeedsAdditionalDepot);

        var shortage = Assert.Single(section.SupplyShortages);
        Assert.Equal(2, shortage.ItemId);
        Assert.Equal("Nuoc sach", shortage.ItemName);
        Assert.Equal(9, shortage.SelectedDepotId);
        Assert.Equal(15, shortage.MissingQuantity);

        var resource = Assert.Single(section.SuggestedResources);
        Assert.Equal("TEAM", resource.ResourceType);

        var activity = Assert.Single(section.SuggestedActivities);
        Assert.Equal("snake-activity", activity.Description);
        Assert.Equal("COLLECT_SUPPLIES", activity.ActivityType);
        Assert.Equal(9, activity.DepotId);
    }

    private static MissionAiSuggestionSection? InvokeFrom(MissionAiSuggestionModel model)
    {
        var method = typeof(MissionAiSuggestionSection).GetMethod(
            "From",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return (MissionAiSuggestionSection?)method!.Invoke(null, [model]);
    }

    private static ActivityAiSuggestionModel CreateActivitySuggestion(string phase, string description)
    {
        return new ActivityAiSuggestionModel
        {
            SuggestionPhase = phase,
            SuggestedActivities = JsonSerializer.Serialize(new List<SuggestedActivityDto>
            {
                new()
                {
                    Step = 1,
                    ActivityType = "DELIVER_SUPPLIES",
                    Description = description
                }
            })
        };
    }

    private static ActivityAiSuggestionModel CreateSnakeCaseActivitySuggestion(string phase, string description)
    {
        return new ActivityAiSuggestionModel
        {
            SuggestionPhase = phase,
            SuggestedActivities = $$"""
                [
                  {
                    "step": 1,
                    "activity_type": "COLLECT_SUPPLIES",
                    "description": "{{description}}",
                    "depot_id": 9,
                    "depot_name": "Kho A",
                    "estimated_time": "25 phut"
                  }
                ]
                """
        };
    }
}
