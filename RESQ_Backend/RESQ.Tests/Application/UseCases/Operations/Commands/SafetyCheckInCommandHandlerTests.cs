using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Commands.SafetyCheckIn;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class SafetyCheckInCommandHandlerTests
{
    private static readonly Guid MemberId = Guid.Parse("aaaaaaaa-4444-4444-4444-444444444444");

    [Fact]
    public async Task Handle_PushesMissionExecutionRealtime_WhenTeamChecksIn()
    {
        var missionTeamRepository = new StubMissionTeamRepository(BuildMissionTeam());
        var missionRepository = new StubMissionRepository(new MissionModel { Id = 12, Status = MissionStatus.OnGoing });
        var activityRepository = new StubMissionActivityRepository(
        [
            new MissionActivityModel
            {
                Id = 90,
                MissionId = 12,
                MissionTeamId = 34,
                Status = MissionActivityStatus.OnGoing,
                EstimatedTime = 60
            }
        ]);
        var adminRealtimeHubService = new StubAdminRealtimeHubService();
        var handler = new SafetyCheckInCommandHandler(
            missionTeamRepository,
            missionRepository,
            activityRepository,
            adminRealtimeHubService,
            new StubUnitOfWork());

        var result = await handler.Handle(
            new SafetyCheckInCommand(12, 34, MemberId),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(34, missionTeamRepository.LastSafetyUpdateId);
        Assert.Equal("Safe", missionTeamRepository.LastSafetyStatus);

        var realtimeUpdate = Assert.Single(adminRealtimeHubService.MissionExecutionProgressUpdates);
        Assert.Equal("TeamSafetyCheckIn", realtimeUpdate.Action);
        Assert.Equal(12, realtimeUpdate.MissionId);
        Assert.Equal(34, realtimeUpdate.MissionTeamId);
        Assert.Equal(56, realtimeUpdate.RescueTeamId);
        Assert.Equal("Safe", realtimeUpdate.Status);
        Assert.Equal(MemberId, realtimeUpdate.ChangedBy);
        Assert.NotNull(realtimeUpdate.SafetyLatestCheckInAt);
        Assert.NotNull(realtimeUpdate.SafetyTimeoutAt);
        Assert.True(realtimeUpdate.RequeryRecommended);
    }

    private static MissionTeamModel BuildMissionTeam() => new()
    {
        Id = 34,
        MissionId = 12,
        RescuerTeamId = 56,
        RescueTeamMembers =
        [
            new MissionTeamMemberInfo { UserId = MemberId }
        ]
    };

    private sealed class StubMissionTeamRepository(MissionTeamModel missionTeam) : IMissionTeamRepository
    {
        public int? LastSafetyUpdateId { get; private set; }
        public DateTime? LastLatestCheckInAt { get; private set; }
        public DateTime? LastTimeoutAt { get; private set; }
        public string? LastSafetyStatus { get; private set; }

        public Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<MissionTeamModel?>(missionTeam);

        public Task UpdateSafetyStateAsync(
            int id,
            DateTime? latestCheckInAt,
            DateTime? timeoutAt,
            string? safetyStatus,
            CancellationToken cancellationToken = default)
        {
            LastSafetyUpdateId = id;
            LastLatestCheckInAt = latestCheckInAt;
            LastTimeoutAt = timeoutAt;
            LastSafetyStatus = safetyStatus;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<int> CreateAsync(MissionTeamModel model, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int id, string status, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateCurrentLocationAsync(int id, double latitude, double longitude, string locationSource, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<MissionTeamModel?> GetByMissionAndTeamAsync(int missionId, int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult<MissionTeamModel?>(null);
    }

    private sealed class StubMissionRepository(MissionModel mission) : IMissionRepository
    {
        public Task<MissionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<MissionModel?>(mission);
        public Task<IEnumerable<MissionModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<IEnumerable<MissionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<IEnumerable<MissionModel>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<int> CreateAsync(MissionModel mission, Guid createdBy, CancellationToken cancellationToken = default) => Task.FromResult(mission.Id);
        public Task UpdateAsync(MissionModel mission, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int missionId, MissionStatus status, bool completed, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubMissionActivityRepository(IReadOnlyCollection<MissionActivityModel> activities) : IMissionActivityRepository
    {
        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(activities.FirstOrDefault(activity => activity.Id == id));
        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) => Task.FromResult(activities.Where(activity => activity.MissionId == missionId).AsEnumerable());
        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionActivityModel>());
        public Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MissionActivityModel>>([]);
        public Task<int> AddAsync(MissionActivityModel activity, CancellationToken cancellationToken = default) => Task.FromResult(activity.Id);
        public Task UpdateAsync(MissionActivityModel activity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int activityId, MissionActivityStatus status, Guid decisionBy, string? imageUrl = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AssignTeamAsync(int activityId, int missionTeamId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> activityIds, Guid decisionBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
