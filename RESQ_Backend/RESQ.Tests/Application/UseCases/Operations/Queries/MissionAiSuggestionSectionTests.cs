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
                  "mixed_rescue_relief_warning": "Ke hoach dang gop chung cuu ho/cap cuu voi cuu tro cap phat.",
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
                CreateMixedSnakeCaseActivitySuggestion("Validated")
            ]
        };

        var section = InvokeFrom(model);

        Assert.NotNull(section);
        Assert.Equal("[SOS ID 1]: need water", section!.OverallAssessment);
        Assert.Equal("1 gio 5 phut", section.EstimatedDuration);
        Assert.Equal("Coordinator needs backup stock", section.SpecialNotes);
        Assert.Equal(
            "Ke hoach dang gop chung cuu ho/cap cuu voi cuu tro cap phat.",
            section.MixedRescueReliefWarning);
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

        var collectActivity = Assert.Single(section.SuggestedActivities, activity => activity.ActivityType == "COLLECT_SUPPLIES");
        var rescueActivity = Assert.Single(section.SuggestedActivities, activity => activity.ActivityType == "RESCUE");
        Assert.Equal("snake-activity", collectActivity.Description);
        Assert.Equal(9, collectActivity.DepotId);
        Assert.Equal(91, rescueActivity.SosRequestId);
    }

    [Fact]
    public void From_BackfillsMixedRescueReliefWarning_FromLegacySpecialNotes()
    {
        var model = new MissionAiSuggestionModel
        {
            Id = 105,
            Metadata = """
                {
                  "special_notes": "Coordinator note\nKe hoach dang gop chung cuu ho/cap cuu voi cuu tro cap phat. Nguyen tac an toan: sau khi cuu nan nhan phai dua ho ve Safe Zone/Assembly Point ngay de cap cuu, khong tiep tuc cho nan nhan di phat do. Khuyen nghi tach thanh mission rieng; coordinator chi nen bo qua canh bao nay khi chu dong chap nhan trach nhiem."
                }
                """
        };

        var section = InvokeFrom(model);

        Assert.NotNull(section);
        Assert.Equal("Coordinator note", section!.SpecialNotes);
        Assert.Equal(MissionSuggestionWarningHelper.MixedRescueReliefWarningMessage, section.MixedRescueReliefWarning);
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

    private static ActivityAiSuggestionModel CreateMixedSnakeCaseActivitySuggestion(string phase)
    {
        return new ActivityAiSuggestionModel
        {
            SuggestionPhase = phase,
            SuggestedActivities = """
                [
                  {
                    "step": 1,
                    "activity_type": "COLLECT_SUPPLIES",
                    "description": "snake-activity",
                    "depot_id": 9,
                    "depot_name": "Kho A",
                    "estimated_time": "25 phut",
                    "sos_request_id": 77
                  },
                  {
                    "step": 2,
                    "activity_type": "RESCUE",
                    "description": "dua nan nhan ra khoi khu vuc nguy hiem",
                    "estimated_time": "15 phut",
                    "sos_request_id": 91
                  }
                ]
                """
        };
    }
}
