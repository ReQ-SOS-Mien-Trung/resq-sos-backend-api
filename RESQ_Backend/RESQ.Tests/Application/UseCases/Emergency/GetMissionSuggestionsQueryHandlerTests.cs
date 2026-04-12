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
                    SuggestedPriorityScore = 9.5,
                    ConfidenceScore = 0.95,
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
        var activityGroup = Assert.Single(mission.Activities);
        var activity = Assert.Single(activityGroup.SuggestedActivities);

        Assert.Equal("COLLECT_SUPPLIES", activity.ActivityType);
        Assert.Equal("25 phut", activity.EstimatedTime);
        Assert.Equal("SplitAcrossTeams", activity.ExecutionMode);
        Assert.Equal(2, activity.RequiredTeamCount);
        Assert.Equal("cluster-5-main", activity.CoordinationGroupKey);
        Assert.Equal("Can 2 doi chia nhanh", activity.CoordinationNotes);
        Assert.Equal(77, activity.SosRequestId);
        Assert.Equal(9, activity.DepotId);
        Assert.Equal("Kho A", activity.DepotName);
        Assert.Equal("72 Phan Dinh Phung", activity.DepotAddress);
        Assert.Equal(4, activity.AssemblyPointId);
        Assert.Equal("Nha thi dau", activity.AssemblyPointName);
        Assert.Equal(18.35, activity.AssemblyPointLatitude);
        Assert.Equal(105.9, activity.AssemblyPointLongitude);
        Assert.Equal("Kho A", activity.DestinationName);
        Assert.Equal(18.351, activity.DestinationLatitude);
        Assert.Equal(105.901, activity.DestinationLongitude);

        var supply = Assert.Single(activity.SuppliesToCollect!);
        Assert.Equal(11, supply.ItemId);
        Assert.Equal("Nuoc tinh khiet", supply.ItemName);
        Assert.Equal(120, supply.Quantity);
        Assert.Equal("chai", supply.Unit);

        Assert.NotNull(activity.SuggestedTeam);
        Assert.Equal(15, activity.SuggestedTeam!.TeamId);
        Assert.Equal("Doi co dong 1", activity.SuggestedTeam.TeamName);
        Assert.Equal("Rescue", activity.SuggestedTeam.TeamType);
        Assert.Equal("Gan nhat", activity.SuggestedTeam.Reason);
        Assert.Equal("AP Ha Tinh", activity.SuggestedTeam.AssemblyPointName);
        Assert.Equal(18.36, activity.SuggestedTeam.Latitude);
        Assert.Equal(105.89, activity.SuggestedTeam.Longitude);
        Assert.Equal(1.8, activity.SuggestedTeam.DistanceKm);
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
    }
}
