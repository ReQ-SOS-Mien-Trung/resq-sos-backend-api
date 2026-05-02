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
    public void ApplyNearbyTeamConstraints_RewritesTechnicalSuggestedTeamReason()
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
                        TeamId = 1,
                        TeamName = "Đội Hương Giang 1",
                        Reason = "Đội nằm trong pool nearby teams của cluster, cách tâm cluster khoảng 0.57 km."
                    }
                }
            ]
        };

        RescueMissionSuggestionReviewHelper.ApplyNearbyTeamConstraints(
            result,
            [
                new AgentTeamInfo
                {
                    TeamId = 1,
                    TeamName = "Đội Hương Giang 1",
                    TeamType = "Mixed",
                    AssemblyPointId = 1,
                    AssemblyPointName = "Sân vận động Tự Do",
                    DistanceKm = 0.57
                }
            ]);

        var activity = Assert.Single(result.SuggestedActivities);
        var reason = activity.SuggestedTeam!.Reason;

        Assert.Equal(
            "Đội đang sẵn sàng và ở gần khu vực cần hỗ trợ, cách trung tâm khu vực khoảng 0.57 km.",
            reason);
        Assert.DoesNotContain("pool", reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nearby", reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cluster", reason, StringComparison.OrdinalIgnoreCase);
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
    public void ApplyNearbyDepotConstraints_InheritsDepotForDeliverFromCollectRoute()
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
                    DepotName = "Kho Hue",
                    SuppliesToCollect =
                    [
                        new SupplyToCollectDto
                        {
                            ItemName = "Nuoc sach",
                            Quantity = 1
                        }
                    ]
                },
                new SuggestedActivityDto
                {
                    Step = 2,
                    ActivityType = "DELIVER_SUPPLIES",
                    SosRequestId = 23,
                    SuppliesToCollect =
                    [
                        new SupplyToCollectDto
                        {
                            ItemName = "Nuoc sach",
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
                    Name = "Kho Hue",
                    Address = "1 Le Loi"
                }
            ]);

        var deliver = result.SuggestedActivities.Single(activity => activity.ActivityType == "DELIVER_SUPPLIES");
        Assert.Equal(1, deliver.DepotId);
        Assert.Equal("Kho Hue", deliver.DepotName);
        Assert.Equal("1 Le Loi", deliver.DepotAddress);
        Assert.False(result.NeedsManualReview);
        Assert.DoesNotContain("depot", result.SpecialNotes ?? string.Empty, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("ngoài pool", result.SpecialNotes);
    }
}
