using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Queries.GetMissionById;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;
using RESQ.Application.UseCases.Operations.Queries.GetMyTeamMissions;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Identity;
using RESQ.Domain.Enum.Logistics;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Tests.Application.UseCases.Operations.Queries;

public class MissionSafetyCheckQueryHandlerTests
{
    [Fact]
    public async Task GetMissions_PopulatesMissionAndTeamSafetyCheck()
    {
        var mission = CreateMission(10);
        var team = CreateTeam(mission.Id, safetyStatus: "Safe");

        var handler = new GetMissionsQueryHandler(
            new StubMissionRepository([mission]),
            new StubMissionTeamRepository([team]),
            new StubMissionAiSuggestionRepository(),
            new StubSosRequestRepository(),
            new StubSosRequestUpdateRepository(),
            new StubItemModelMetadataRepository(),
            NullLogger<GetMissionsQueryHandler>.Instance);

        var response = await handler.Handle(new GetMissionsQuery(null), CancellationToken.None);

        var missionDto = Assert.Single(response.Missions);
        AssertSafetyCheck(missionDto, "Safe");
    }

    [Fact]
    public async Task GetMissionById_PopulatesMissionAndTeamSafetyCheck()
    {
        var mission = CreateMission(11);
        var team = CreateTeam(mission.Id, safetyStatus: "AtRisk");

        var handler = new GetMissionByIdQueryHandler(
            new StubMissionRepository([mission]),
            new StubMissionTeamRepository([team]),
            new StubMissionAiSuggestionRepository(),
            new StubSosRequestRepository(),
            new StubSosRequestUpdateRepository(),
            new StubItemModelMetadataRepository(),
            NullLogger<GetMissionByIdQueryHandler>.Instance);

        var missionDto = await handler.Handle(new GetMissionByIdQuery(mission.Id), CancellationToken.None);

        Assert.NotNull(missionDto);
        AssertSafetyCheck(missionDto!, "AtRisk");
    }

    [Fact]
    public async Task GetMyTeamMissions_PopulatesMissionAndTeamSafetyCheck()
    {
        var userId = Guid.NewGuid();
        var rescueTeamId = 5;
        var mission = CreateMission(12);
        var assignment = CreateTeam(mission.Id, rescueTeamId, safetyStatus: "Safe");

        var activeTeam = RescueTeamModel.Create("Team A", RescueTeamType.Rescue, assemblyPointId: 1, managedBy: Guid.NewGuid());
        activeTeam.SetId(rescueTeamId);

        var handler = new GetMyTeamMissionsQueryHandler(
            new StubPersonnelQueryRepository(activeTeam),
            new StubMissionRepository([mission]),
            new StubMissionTeamRepository([assignment]),
            new StubSosRequestRepository(),
            new StubSosRequestUpdateRepository(),
            new StubItemModelMetadataRepository());

        var response = await handler.Handle(new GetMyTeamMissionsQuery(userId), CancellationToken.None);

        var missionDto = Assert.Single(response.Missions);
        AssertSafetyCheck(missionDto, "Safe");
    }

    private static MissionModel CreateMission(int id) => new()
    {
        Id = id,
        Status = MissionStatus.OnGoing,
        Activities = []
    };

    private static MissionTeamModel CreateTeam(
        int missionId,
        int rescueTeamId = 5,
        string safetyStatus = "Safe")
    {
        return new MissionTeamModel
        {
            Id = missionId + 100,
            MissionId = missionId,
            RescuerTeamId = rescueTeamId,
            SafetyStatus = safetyStatus,
            SafetyLatestCheckInAt = DateTime.UtcNow.AddMinutes(-10),
            SafetyTimeoutAt = DateTime.UtcNow.AddMinutes(60),
            Status = "Assigned"
        };
    }

    private static void AssertSafetyCheck(MissionDto missionDto, string expectedOverallStatus)
    {
        Assert.NotNull(missionDto.SafetyCheck);
        Assert.Equal(expectedOverallStatus, missionDto.SafetyCheck!.OverallStatus);
        Assert.True(missionDto.SafetyCheck.IsMonitoringActive);
        Assert.Equal(1, missionDto.SafetyCheck.TotalTeams);

        var teamDto = Assert.Single(missionDto.Teams);
        Assert.NotNull(teamDto.SafetyCheck);
        Assert.Equal(expectedOverallStatus, teamDto.SafetyCheck!.Status);
        Assert.True(teamDto.SafetyCheck.IsMonitoringActive);
        Assert.Equal(teamDto.MissionTeamId, teamDto.SafetyCheck.MissionTeamId);
        Assert.Equal(teamDto.RescueTeamId, teamDto.SafetyCheck.RescueTeamId);
    }

    private sealed class StubMissionRepository(IEnumerable<MissionModel> missions) : IMissionRepository
    {
        private readonly List<MissionModel> _missions = missions.ToList();

        public Task<MissionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_missions.FirstOrDefault(mission => mission.Id == id));

        public Task<IEnumerable<MissionModel>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_missions.AsEnumerable());

        public Task<IEnumerable<MissionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_missions.Where(mission => mission.ClusterId == clusterId).AsEnumerable());

        public Task<IEnumerable<MissionModel>> GetByIdsAsync(IEnumerable<int> missionIds, CancellationToken cancellationToken = default)
        {
            var idSet = missionIds.ToHashSet();
            return Task.FromResult(_missions.Where(mission => idSet.Contains(mission.Id)).AsEnumerable());
        }

        public Task<int> CreateAsync(MissionModel mission, Guid coordinatorId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(MissionModel mission, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusAsync(int missionId, MissionStatus status, bool isCompleted, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubMissionTeamRepository(IEnumerable<MissionTeamModel> teams) : IMissionTeamRepository
    {
        private readonly List<MissionTeamModel> _teams = teams.ToList();

        public Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_teams.FirstOrDefault(team => team.Id == id));

        public Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_teams.Where(team => team.MissionId == missionId).AsEnumerable());

        public Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rescuerTeamId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_teams.Where(team => team.RescuerTeamId == rescuerTeamId).AsEnumerable());

        public Task<MissionTeamModel?> GetByMissionAndTeamAsync(int missionId, int rescuerTeamId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_teams.FirstOrDefault(team => team.MissionId == missionId && team.RescuerTeamId == rescuerTeamId));

        public Task<int> CreateAsync(MissionTeamModel model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusAsync(int id, string status, string? note, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateSafetyStateAsync(int id, DateTime? latestCheckInAt, DateTime? timeoutAt, string? safetyStatus, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateCurrentLocationAsync(int id, double latitude, double longitude, string locationSource, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubMissionAiSuggestionRepository : IMissionAiSuggestionRepository
    {
        public Task<int> CreateAsync(MissionAiSuggestionModel model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(MissionAiSuggestionModel model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SavePipelineSnapshotAsync(MissionSuggestionMetadata metadata, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SavePipelineSnapshotAsync(int suggestionId, MissionSuggestionMetadata metadata, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MissionAiSuggestionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<MissionAiSuggestionModel?>(null);
        public Task<IEnumerable<MissionAiSuggestionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionAiSuggestionModel>());
        public Task<IEnumerable<MissionAiSuggestionModel>> GetByClusterIdsAsync(IEnumerable<int> clusterIds, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionAiSuggestionModel>());
    }

    private sealed class StubPersonnelQueryRepository(RescueTeamModel? activeTeam) : IPersonnelQueryRepository
    {
        public Task<RescueTeamModel?> GetActiveRescueTeamByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(activeTeam);

        public Task<PagedResult<FreeRescuerModel>> GetFreeRescuersAsync(int pageNumber, int pageSize, string? firstName = null, string? lastName = null, string? phone = null, string? email = null, RescuerType? rescuerType = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<RescueTeamModel>> GetAllRescueTeamsAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RescueTeamModel?> GetRescueTeamDetailAsync(int teamId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<FreeRescuerModel>> GetRescuersByAssemblyPointAsync(int assemblyPointId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<RescueTeamModel>> GetAllAvailableTeamsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<RescuerModel>> GetRescuersAsync(int pageNumber, int pageSize, bool? hasAssemblyPoint = null, bool? hasTeam = null, RescuerType? rescuerType = null, string? abilitySubgroupCode = null, string? abilityCategoryCode = null, string? search = null, List<string>? assemblyPointCodes = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubSosRequestRepository : ISosRequestRepository
    {
        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<SosRequestModel?>(null);
        public Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<SosRequestModel>> GetAllPagedAsync(int pageNumber, int pageSize, IReadOnlyCollection<SosRequestStatus>? statuses = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusAsync(int id, SosRequestStatus status, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusByClusterIdAsync(int clusterId, SosRequestStatus status, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubSosRequestUpdateRepository : ISosRequestUpdateRepository
    {
        public Task AddVictimUpdateAsync(SosRequestVictimUpdateModel update, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> updates, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(IEnumerable<int> teamIncidentIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubItemModelMetadataRepository : IItemModelMetadataRepository
    {
        public Task<List<MetadataDto>> GetAllForMetadataAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<MetadataDto>> GetByCategoryCodeAsync(ItemCategoryCode categoryCode, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<DonationImportItemInfo>> GetAllForDonationTemplateAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<DonationImportTargetGroupInfo>> GetAllTargetGroupsForTemplateAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Dictionary<int, ItemModelRecord>> GetByIdsAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<int, ItemModelRecord>());
        public Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasInventoryTransactionsAsync(int itemModelId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> UpdateItemModelAsync(ItemModelRecord model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
