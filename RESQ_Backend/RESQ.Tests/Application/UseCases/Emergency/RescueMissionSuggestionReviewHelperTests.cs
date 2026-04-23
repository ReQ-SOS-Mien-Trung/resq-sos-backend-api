using RESQ.Application.Services;
using RESQ.Application.UseCases.Emergency.Shared;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class RescueMissionSuggestionReviewHelperTests
{
    [Fact]
    public void ApplyNearbyTeamConstraints_RecoversCanonicalTeamByName_WhenIdIsHallucinated()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto
                {
                    Step = 1,
                    ActivityType = "RESCUE",
                    SuggestedTeam = new SuggestedTeamDto
                    {
                        TeamId = 101,
                        TeamName = "Đội Phản ứng nhanh Y tế Huế",
                        Reason = "Gần hiện trường"
                    }
                }
            ]
        };

        RescueMissionSuggestionReviewHelper.ApplyNearbyTeamConstraints(
            result,
            [
                new AgentTeamInfo
                {
                    TeamId = 2,
                    TeamName = "Đội Phản ứng nhanh Y tế Huế",
                    TeamType = "Medical",
                    AssemblyPointId = 1,
                    AssemblyPointName = "Sân vận động Tự Do",
                    DistanceKm = 2.4
                }
            ]);

        var activity = Assert.Single(result.SuggestedActivities);
        Assert.NotNull(activity.SuggestedTeam);
        Assert.Equal(2, activity.SuggestedTeam!.TeamId);
        Assert.Equal("Đội Phản ứng nhanh Y tế Huế", activity.SuggestedTeam.TeamName);
        Assert.False(result.NeedsManualReview);
    }

    [Fact]
    public void ApplyNearbyDepotConstraints_RecoversCanonicalDepotByName_WhenIdIsHallucinated()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto
                {
                    Step = 1,
                    ActivityType = "COLLECT_SUPPLIES",
                    DepotId = 999,
                    DepotName = "Uỷ Ban MTTQVN Tỉnh Thừa Thiên Huế",
                    SuppliesToCollect =
                    [
                        new SupplyToCollectDto
                        {
                            ItemName = "Áo phao cứu sinh",
                            Quantity = 1
                        }
                    ]
                }
            ]
        };

        RescueMissionSuggestionReviewHelper.ApplyNearbyDepotConstraints(
            result,
            [
                new DepotSummary
                {
                    Id = 1,
                    Name = "Uỷ Ban MTTQVN Tỉnh Thừa Thiên Huế",
                    Address = "46 Đống Đa, TP. Huế"
                }
            ]);

        var activity = Assert.Single(result.SuggestedActivities);
        Assert.Equal(1, activity.DepotId);
        Assert.Equal("Uỷ Ban MTTQVN Tỉnh Thừa Thiên Huế", activity.DepotName);
        Assert.Equal("46 Đống Đa, TP. Huế", activity.DepotAddress);
        Assert.False(result.NeedsManualReview);
    }

    [Fact]
    public void ApplyNearbyDepotConstraints_ClearsDepotOutsideScope_AndFlagsManualReview()
    {
        var result = new RescueMissionSuggestionResult
        {
            SuggestedActivities =
            [
                new SuggestedActivityDto
                {
                    Step = 1,
                    ActivityType = "COLLECT_SUPPLIES",
                    DepotId = 88,
                    DepotName = "Kho ảo ngoài vùng",
                    DestinationName = "Kho ảo ngoài vùng",
                    DestinationLatitude = 16.45,
                    DestinationLongitude = 107.56,
                    SuppliesToCollect =
                    [
                        new SupplyToCollectDto
                        {
                            ItemName = "Xuồng cao su cứu hộ",
                            Quantity = 1
                        }
                    ]
                }
            ],
            SupplyShortages =
            [
                new SupplyShortageDto
                {
                    ItemName = "Xuồng cao su cứu hộ",
                    SelectedDepotId = 88,
                    SelectedDepotName = "Kho ảo ngoài vùng"
                }
            ]
        };

        RescueMissionSuggestionReviewHelper.ApplyNearbyDepotConstraints(
            result,
            [
                new DepotSummary
                {
                    Id = 1,
                    Name = "Kho hợp lệ",
                    Address = "1 Lê Lợi"
                }
            ]);

        var activity = Assert.Single(result.SuggestedActivities);
        Assert.Null(activity.DepotId);
        Assert.Null(activity.DepotName);
        Assert.Null(activity.DepotAddress);
        Assert.Null(activity.DestinationName);
        Assert.Null(activity.DestinationLatitude);
        Assert.Null(activity.DestinationLongitude);

        var shortage = Assert.Single(result.SupplyShortages);
        Assert.Null(shortage.SelectedDepotId);
        Assert.Null(shortage.SelectedDepotName);
        Assert.True(result.NeedsManualReview);
        Assert.Contains("ngoài pool nearby depots", result.SpecialNotes);
    }
}
