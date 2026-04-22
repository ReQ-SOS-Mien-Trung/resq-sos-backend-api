using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Operations;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Personnel;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Shared;

public class RescueTeamMissionLifecycleSyncServiceTests
{
    [Fact]
    public async Task SyncTeamsToOnMissionAsync_StartsAssignedTeams_AndSkipsTeamsAlreadyOnMission()
    {
        var assignedTeam = BuildTeam(1, RescueTeamStatus.Assigned);
        var onMissionTeam = BuildTeam(2, RescueTeamStatus.OnMission);
        var repository = new RecordingRescueTeamRepository(assignedTeam, onMissionTeam);
        var hubService = new RecordingOperationalHubService();
        var service = BuildService(repository, hubService);

        var result = await service.SyncTeamsToOnMissionAsync([1, 2, 1], CancellationToken.None);
        await service.PushRealtimeIfNeededAsync(result, CancellationToken.None);

        Assert.True(result.HasChanges);
        Assert.Equal([1], result.ChangedTeamIds);
        Assert.Equal(RescueTeamStatus.OnMission, repository.Get(1).Status);
        Assert.Equal(RescueTeamStatus.OnMission, repository.Get(2).Status);
        Assert.Equal([1], repository.UpdatedTeamIds);
        Assert.Single(hubService.LogisticsUpdates);
    }

    [Fact]
    public async Task SyncTeamsToOnMissionAsync_ThrowsConflict_WhenAnyTeamIsNotAssigned()
    {
        var repository = new RecordingRescueTeamRepository(
            BuildTeam(1, RescueTeamStatus.Assigned),
            BuildTeam(2, RescueTeamStatus.Available));
        var service = BuildService(repository, new RecordingOperationalHubService());

        var exception = await Assert.ThrowsAsync<RESQ.Application.Exceptions.ConflictException>(() =>
            service.SyncTeamsToOnMissionAsync([1, 2], CancellationToken.None));

        Assert.Contains("Đội #2", exception.Message);
        Assert.Empty(repository.UpdatedTeamIds);
        Assert.Equal(RescueTeamStatus.Assigned, repository.Get(1).Status);
    }

    [Fact]
    public async Task SyncTeamToAvailableAfterReturnAsync_FinishesOnMissionTeam()
    {
        var repository = new RecordingRescueTeamRepository(BuildTeam(1, RescueTeamStatus.OnMission));
        var service = BuildService(repository, new RecordingOperationalHubService());

        var result = await service.SyncTeamToAvailableAfterReturnAsync(1, CancellationToken.None);

        Assert.True(result.HasChanges);
        Assert.Equal([1], result.ChangedTeamIds);
        Assert.Equal(RescueTeamStatus.Available, repository.Get(1).Status);
        Assert.Equal([1], repository.UpdatedTeamIds);
    }

    [Fact]
    public async Task SyncTeamToAvailableAfterReturnAsync_NormalizesAssignedTeamToAvailable()
    {
        var repository = new RecordingRescueTeamRepository(BuildTeam(1, RescueTeamStatus.Assigned));
        var service = BuildService(repository, new RecordingOperationalHubService());

        var result = await service.SyncTeamToAvailableAfterReturnAsync(1, CancellationToken.None);

        Assert.True(result.HasChanges);
        Assert.Equal(RescueTeamStatus.Available, repository.Get(1).Status);
    }

    [Fact]
    public async Task SyncTeamToAvailableAfterReturnAsync_SkipsUnavailableStatuses()
    {
        var repository = new RecordingRescueTeamRepository(BuildTeam(1, RescueTeamStatus.Stuck));
        var service = BuildService(repository, new RecordingOperationalHubService());

        var result = await service.SyncTeamToAvailableAfterReturnAsync(1, CancellationToken.None);

        Assert.False(result.HasChanges);
        Assert.Equal(RescueTeamStatus.Stuck, repository.Get(1).Status);
        Assert.Empty(repository.UpdatedTeamIds);
    }

    [Fact]
    public async Task SyncTeamToAvailableAfterExecutionAsync_NormalizesAssignedTeam_WhenNoNewMissionIsActive()
    {
        var repository = new RecordingRescueTeamRepository(BuildTeam(1, RescueTeamStatus.Assigned));
        var missionTeamRepository = new RecordingMissionTeamRepository(new MissionTeamModel
        {
            Id = 10,
            RescuerTeamId = 1,
            Status = MissionTeamExecutionStatus.CompletedWaitingReport.ToString()
        });
        var service = BuildService(repository, new RecordingOperationalHubService(), missionTeamRepository);

        var result = await service.SyncTeamToAvailableAfterExecutionAsync(1, 10, CancellationToken.None);

        Assert.True(result.HasChanges);
        Assert.Equal(RescueTeamStatus.Available, repository.Get(1).Status);
        Assert.Equal([1], repository.UpdatedTeamIds);
    }

    [Theory]
    [InlineData(nameof(MissionTeamExecutionStatus.Assigned))]
    [InlineData(nameof(MissionTeamExecutionStatus.InProgress))]
    public async Task SyncTeamToAvailableAfterExecutionAsync_DoesNotOverride_WhenTeamHasNewActiveMission(string blockingStatus)
    {
        var repository = new RecordingRescueTeamRepository(BuildTeam(1, RescueTeamStatus.Assigned));
        var missionTeamRepository = new RecordingMissionTeamRepository(
            new MissionTeamModel
            {
                Id = 10,
                RescuerTeamId = 1,
                Status = MissionTeamExecutionStatus.CompletedWaitingReport.ToString()
            },
            new MissionTeamModel
            {
                Id = 11,
                RescuerTeamId = 1,
                Status = blockingStatus
            });
        var service = BuildService(repository, new RecordingOperationalHubService(), missionTeamRepository);

        var result = await service.SyncTeamToAvailableAfterExecutionAsync(1, 10, CancellationToken.None);

        Assert.False(result.HasChanges);
        Assert.Equal(RescueTeamStatus.Assigned, repository.Get(1).Status);
        Assert.Empty(repository.UpdatedTeamIds);
    }

    [Fact]
    public async Task PushRealtimeIfNeededAsync_DoesNotPush_WhenThereAreNoChanges()
    {
        var hubService = new RecordingOperationalHubService();
        var service = BuildService(new RecordingRescueTeamRepository(), hubService);

        await service.PushRealtimeIfNeededAsync(RescueTeamMissionLifecycleSyncResult.None, CancellationToken.None);

        Assert.Empty(hubService.LogisticsUpdates);
    }

    private static RescueTeamMissionLifecycleSyncService BuildService(
        RecordingRescueTeamRepository repository,
        RecordingOperationalHubService hubService,
        RecordingMissionTeamRepository? missionTeamRepository = null) =>
        new(repository, missionTeamRepository ?? new RecordingMissionTeamRepository(), hubService, new StubAdminRealtimeHubService(), NullLogger<RescueTeamMissionLifecycleSyncService>.Instance);

    private static RescueTeamModel BuildTeam(int id, RescueTeamStatus status)
    {
        var leaderId = Guid.Parse($"00000000-0000-0000-0000-{id:D12}");
        var team = RescueTeamModel.Create($"Team {id}", RescueTeamType.Rescue, assemblyPointId: id, managedBy: Guid.NewGuid());
        team.SetId(id);
        team.AddMember(leaderId, isLeader: true, rescuerType: "Core", roleInTeam: "LEADER");
        team.SetAvailableByLeader(leaderId);

        switch (status)
        {
            case RescueTeamStatus.Available:
                return team;
            case RescueTeamStatus.Assigned:
                team.AssignMission();
                return team;
            case RescueTeamStatus.OnMission:
                team.AssignMission();
                team.StartMission();
                return team;
            case RescueTeamStatus.Stuck:
                team.AssignMission();
                team.StartMission();
                team.ReportIncident();
                return team;
            case RescueTeamStatus.Unavailable:
                team.SetUnavailable();
                return team;
            default:
                throw new InvalidOperationException($"Unsupported test status: {status}");
        }
    }

    private sealed class RecordingRescueTeamRepository(params RescueTeamModel[] teams) : IRescueTeamRepository
    {
        private readonly Dictionary<int, RescueTeamModel> _teams = teams.ToDictionary(team => team.Id);

        public List<int> UpdatedTeamIds { get; } = [];

        public RescueTeamModel Get(int teamId) => _teams[teamId];

        public Task<RescueTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_teams.TryGetValue(id, out var team) ? team : null);

        public Task UpdateAsync(RescueTeamModel team, CancellationToken cancellationToken = default)
        {
            _teams[team.Id] = team;
            UpdatedTeamIds.Add(team.Id);
            return Task.CompletedTask;
        }

        public Task<RescueTeamModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<RescueTeamModel>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> IsUserInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> IsLeaderInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Guid?> GetTeamLeaderUserIdByMemberAsync(Guid memberUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> SoftRemoveMemberFromActiveTeamAsync(Guid memberUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasRequiredAbilityCategoryAsync(Guid userId, string categoryCode, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<string?> GetTopAbilityCategoryAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CreateAsync(RescueTeamModel team, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> CountActiveTeamsByAssemblyPointAsync(int assemblyPointId, IEnumerable<int> excludeTeamIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(List<AgentTeamInfo> Teams, int TotalCount)> GetTeamsForAgentAsync(string? abilityKeyword, bool? available, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class RecordingMissionTeamRepository(params MissionTeamModel[] missionTeams) : IMissionTeamRepository
    {
        public Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(missionTeams.FirstOrDefault(missionTeam => missionTeam.Id == id));

        public Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(missionTeams.Where(missionTeam => missionTeam.MissionId == missionId).AsEnumerable());

        public Task<int> CreateAsync(MissionTeamModel model, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateStatusAsync(int id, string status, string? note, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateCurrentLocationAsync(int id, double latitude, double longitude, string locationSource, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rescuerTeamId, CancellationToken cancellationToken = default) =>
            Task.FromResult(missionTeams.Where(missionTeam => missionTeam.RescuerTeamId == rescuerTeamId).AsEnumerable());

        public Task<MissionTeamModel?> GetByMissionAndTeamAsync(int missionId, int rescuerTeamId, CancellationToken cancellationToken = default) =>
            Task.FromResult(missionTeams.FirstOrDefault(missionTeam => missionTeam.MissionId == missionId && missionTeam.RescuerTeamId == rescuerTeamId));
    }

    private sealed class RecordingOperationalHubService : IOperationalHubService
    {
        public List<(string ResourceType, int? ClusterId)> LogisticsUpdates { get; } = [];

        public Task PushLogisticsUpdateAsync(string resourceType, int? clusterId = null, CancellationToken cancellationToken = default)
        {
            LogisticsUpdates.Add((resourceType, clusterId));
            return Task.CompletedTask;
        }

        public Task PushAssemblyPointListUpdateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PushDepotInventoryUpdateAsync(int depotId, string operation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PushSupplyRequestUpdateAsync(SupplyRequestRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PushDepotActivityUpdateAsync(DepotActivityRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PushDepotClosureUpdateAsync(DepotClosureRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
