using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Emergency.Queries.GetMissionSuggestions;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class GetMissionSuggestionsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ParsesSnakeCaseSuggestedActivities()
    {
        var handler = BuildHandler(
            cluster: new SosClusterModel { Id = 5 },
            suggestions:
            [
                new MissionAiSuggestionModel
                {
                    Id = 1,
                    ClusterId = 5,
                    ModelName = "gemini-3.1-flash-lite-preview",
                    AnalysisType = "RescueMissionSuggestion",
                    SuggestedMissionTitle = "Cuu ho khan cap tai Ha Tinh",
                    SuggestedMissionType = "MixedResponse",
                    SuggestedSeverityLevel = "Critical",
                    SuggestedPriorityScore = 9.5,
                    ConfidenceScore = 0.95,
                    Metadata = """
                        {
                          "overall_assessment": "[SOS ID 77]: urgent support is required",
                          "estimated_duration": "2 gio 10 phut",
                          "special_notes": "Split rescue victims to safe zone before any relief delivery.",
                          "mixed_rescue_relief_warning": "Ke hoach dang gop chung cuu ho/cap cuu voi cuu tro cap phat.",
                          "needs_manual_review": true,
                          "low_confidence_warning": "Coordinator should verify the mixed mission plan.",
                          "needs_additional_depot": true,
                          "supply_shortages": [
                            {
                              "sos_request_id": 77,
                              "item_id": 12,
                              "item_name": "Ao phao",
                              "unit": "cai",
                              "selected_depot_id": 9,
                              "selected_depot_name": "Kho A",
                              "needed_quantity": 20,
                              "available_quantity": 8,
                              "missing_quantity": 12,
                              "notes": "selected depot is short"
                            }
                          ],
                          "suggested_resources": [
                            {
                              "resource_type": "VEHICLE",
                              "description": "Xe tai nhe",
                              "quantity": 1,
                              "priority": "Critical"
                            }
                          ]
                        }
                        """,
                    CreatedAt = new DateTime(2026, 4, 12, 2, 35, 9, DateTimeKind.Utc),
                    Activities =
                    [
                        new ActivityAiSuggestionModel
                        {
                            Id = 3,
                            ActivityType = "MIXED",
                            SuggestionPhase = "Execution",
                            ConfidenceScore = 0.95,
                            CreatedAt = new DateTime(2026, 4, 12, 2, 35, 38, DateTimeKind.Utc),
                            SuggestedActivities = """
                                [
                                  {
                                    "step": 1,
                                    "activity_type": "COLLECT_SUPPLIES",
                                    "description": "Di chuyen den kho A",
                                    "priority": "Critical",
                                    "estimated_time": "25 phut",
                                    "execution_mode": "SplitAcrossTeams",
                                    "required_team_count": 2,
                                    "coordination_group_key": "cluster-5-main",
                                    "coordination_notes": "Can 2 doi chia nhanh",
                                    "sos_request_id": 77,
                                    "depot_id": 9,
                                    "depot_name": "Kho A",
                                    "depot_address": "72 Phan Dinh Phung",
                                    "assembly_point_id": 4,
                                    "assembly_point_name": "Nha thi dau",
                                    "assembly_point_latitude": 18.35,
                                    "assembly_point_longitude": 105.9,
                                    "destination_name": "Kho A",
                                    "destination_latitude": 18.351,
                                    "destination_longitude": 105.901,
                                    "supplies_to_collect": [
                                      {
                                        "item_id": 11,
                                        "item_name": "Nuoc tinh khiet",
                                        "quantity": 120,
                                        "unit": "chai"
                                      }
                                    ],
                                    "suggested_team": {
                                      "team_id": 15,
                                      "team_name": "Doi co dong 1",
                                      "team_type": "Rescue",
                                      "reason": "Gan nhat",
                                      "assembly_point_name": "AP Ha Tinh",
                                      "latitude": 18.36,
                                      "longitude": 105.89,
                                      "distance_km": 1.8
                                    }
                                  },
                                  {
                                    "step": 2,
                                    "activity_type": "RESCUE",
                                    "description": "Dua nan nhan ra khoi vung ngap",
                                    "priority": "Critical",
                                    "estimated_time": "15 phut",
                                    "sos_request_id": 91,
                                    "destination_name": "Khu vuc ngap",
                                    "destination_latitude": 18.36,
                                    "destination_longitude": 105.92
                                  }
                                ]
                                """
                        }
                    ]
                }
            ]);

        var response = await handler.Handle(new GetMissionSuggestionsQuery(5), CancellationToken.None);

        Assert.Equal(5, response.ClusterId);
        Assert.Equal(1, response.TotalSuggestions);

        var mission = Assert.Single(response.MissionSuggestions);
        Assert.Equal("MixedResponse", mission.SuggestedMissionType);
        Assert.Equal("Critical", mission.SuggestedSeverityLevel);
        Assert.Equal("[SOS ID 77]: urgent support is required", mission.OverallAssessment);
        Assert.Equal("2 gio 10 phut", mission.EstimatedDuration);
        Assert.Equal("Split rescue victims to safe zone before any relief delivery.", mission.SpecialNotes);
        Assert.Equal(
            "Ke hoach dang gop chung cuu ho/cap cuu voi cuu tro cap phat.",
            mission.MixedRescueReliefWarning);
        Assert.DoesNotContain("Safe Zone", mission.MixedRescueReliefWarning, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Assembly Point", mission.MixedRescueReliefWarning, StringComparison.OrdinalIgnoreCase);
        Assert.True(mission.NeedsManualReview);
        Assert.Equal("Coordinator should verify the mixed mission plan.", mission.LowConfidenceWarning);
        Assert.True(mission.NeedsAdditionalDepot);

        var shortage = Assert.Single(mission.SupplyShortages);
        Assert.Equal(12, shortage.ItemId);
        Assert.Equal("Ao phao", shortage.ItemName);
        Assert.Equal(12, shortage.MissingQuantity);

        var resource = Assert.Single(mission.SuggestedResources);
        Assert.Equal("VEHICLE", resource.ResourceType);
        Assert.Equal("Xe tai nhe", resource.Description);

        var activityGroup = Assert.Single(mission.Activities);
        var collectActivity = Assert.Single(activityGroup.SuggestedActivities, activity => activity.ActivityType == "COLLECT_SUPPLIES");
        var rescueActivity = Assert.Single(activityGroup.SuggestedActivities, activity => activity.ActivityType == "RESCUE");

        Assert.Equal("25 phut", collectActivity.EstimatedTime);
        Assert.Equal("SplitAcrossTeams", collectActivity.ExecutionMode);
        Assert.Equal(2, collectActivity.RequiredTeamCount);
        Assert.Equal("cluster-5-main", collectActivity.CoordinationGroupKey);
        Assert.Equal("Can 2 doi chia nhanh", collectActivity.CoordinationNotes);
        Assert.Equal(77, collectActivity.SosRequestId);
        Assert.Equal(9, collectActivity.DepotId);
        Assert.Equal("Kho A", collectActivity.DepotName);
        Assert.Equal("72 Phan Dinh Phung", collectActivity.DepotAddress);
        Assert.Equal(4, collectActivity.AssemblyPointId);
        Assert.Equal("Nha thi dau", collectActivity.AssemblyPointName);
        Assert.Equal(18.35, collectActivity.AssemblyPointLatitude);
        Assert.Equal(105.9, collectActivity.AssemblyPointLongitude);
        Assert.Equal("Kho A", collectActivity.DestinationName);
        Assert.Equal(18.351, collectActivity.DestinationLatitude);
        Assert.Equal(105.901, collectActivity.DestinationLongitude);

        var supply = Assert.Single(collectActivity.SuppliesToCollect!);
        Assert.Equal(11, supply.ItemId);
        Assert.Equal("Nuoc tinh khiet", supply.ItemName);
        Assert.Equal(120, supply.Quantity);
        Assert.Equal("chai", supply.Unit);

        Assert.NotNull(collectActivity.SuggestedTeam);
        Assert.Equal(15, collectActivity.SuggestedTeam!.TeamId);
        Assert.Equal("Doi co dong 1", collectActivity.SuggestedTeam.TeamName);
        Assert.Equal("Rescue", collectActivity.SuggestedTeam.TeamType);
        Assert.Equal("Gan nhat", collectActivity.SuggestedTeam.Reason);
        Assert.Equal("AP Ha Tinh", collectActivity.SuggestedTeam.AssemblyPointName);
        Assert.Equal(18.36, collectActivity.SuggestedTeam.Latitude);
        Assert.Equal(105.89, collectActivity.SuggestedTeam.Longitude);
        Assert.Equal(1.8, collectActivity.SuggestedTeam.DistanceKm);
        Assert.Equal(91, rescueActivity.SosRequestId);
        Assert.Equal("15 phut", rescueActivity.EstimatedTime);
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenClusterDoesNotExist()
    {
        var handler = BuildHandler(cluster: null, suggestions: []);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetMissionSuggestionsQuery(5), CancellationToken.None));
    }

    private static GetMissionSuggestionsQueryHandler BuildHandler(
        SosClusterModel? cluster,
        IEnumerable<MissionAiSuggestionModel> suggestions)
    {
        return new GetMissionSuggestionsQueryHandler(
            new StubMissionAiSuggestionRepository(suggestions),
            new StubSosClusterRepository(cluster),
            NullLogger<GetMissionSuggestionsQueryHandler>.Instance);
    }

    private sealed class StubMissionAiSuggestionRepository(IEnumerable<MissionAiSuggestionModel> suggestions)
        : IMissionAiSuggestionRepository
    {
        private readonly List<MissionAiSuggestionModel> _suggestions = suggestions.ToList();

        public Task<int> CreateAsync(MissionAiSuggestionModel model, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateAsync(MissionAiSuggestionModel model, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task SavePipelineSnapshotAsync(int suggestionId, MissionSuggestionMetadata metadata, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<MissionAiSuggestionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_suggestions.FirstOrDefault(suggestion => suggestion.Id == id));

        public Task<IEnumerable<MissionAiSuggestionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default)
            => Task.FromResult(_suggestions.Where(suggestion => suggestion.ClusterId == clusterId).AsEnumerable());

        public Task<IEnumerable<MissionAiSuggestionModel>> GetByClusterIdsAsync(IEnumerable<int> clusterIds, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<MissionAiSuggestionModel>());
    }

    private sealed class StubSosClusterRepository(SosClusterModel? cluster) : ISosClusterRepository
    {
        public Task<SosClusterModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(cluster?.Id == id ? cluster : null);

        public Task<IEnumerable<SosClusterModel>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<SosClusterModel>());

        public Task<int> CreateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
