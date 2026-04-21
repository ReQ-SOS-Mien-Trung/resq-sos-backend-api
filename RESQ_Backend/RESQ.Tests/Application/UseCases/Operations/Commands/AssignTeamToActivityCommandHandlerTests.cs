using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToActivity;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToMission;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Emergency;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class AssignTeamToActivityCommandHandlerTests
{
    private static readonly Guid CoordinatorId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    // ─── Activity not found ───────────────────────────────────────

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenActivityDoesNotExist()
    {
        var handler = BuildHandler(activityRepo: new StubActivityRepo(null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(BuildCommand(), CancellationToken.None));
    }

    // ─── Team already in mission → assigns directly ───────────────

    [Fact]
    public async Task Handle_TeamAlreadyInMission_AssignsDirectly()
    {
        var existingTeam = new MissionTeamModel { Id = 5, MissionId = 10, RescuerTeamId = 3, TeamName = "Alpha" };
        var missionTeamRepo = new StubMissionTeamRepo(existingTeam);
        var activityRepo = new StubActivityRepo(BuildActivity());

        var handler = BuildHandler(activityRepo: activityRepo, missionTeamRepo: missionTeamRepo);
        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.Equal(5, result.MissionTeamId);
        Assert.Equal(3, result.RescueTeamId);
        Assert.Equal("Alpha", result.TeamName);
    }

    // ─── Team not in mission → calls AssignTeamToMission ──────────

    [Fact]
    public async Task Handle_TeamNotInMission_CallsMediator()
    {
        var mediator = new RecordingMediator(r => r switch
        {
            AssignTeamToMissionCommand => new AssignTeamToMissionResponse
            {
                MissionTeamId = 7,
                MissionId = 10,
                RescueTeamId = 3,
                AssignedAt = DateTime.UtcNow
            },
            _ => null
        });

        var handler = BuildHandler(
            activityRepo: new StubActivityRepo(BuildActivity()),
            missionTeamRepo: new StubMissionTeamRepo(null),
            mediator: mediator);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.Equal(7, result.MissionTeamId);
        Assert.Null(result.TeamName); // team not known yet
        Assert.Contains(mediator.SentRequests, r => r is AssignTeamToMissionCommand);
    }

    // ─── With SosRequestId → syncs incident SOS status ────────────

    [Fact]
    public async Task Handle_WithSosRequestId_CallsSyncAndSucceeds()
    {
        var activity = BuildActivity(sosRequestId: 100);
        var existingTeam = new MissionTeamModel { Id = 5, MissionId = 10, RescuerTeamId = 3 };

        var handler = BuildHandler(
            activityRepo: new StubActivityRepo(activity),
            missionTeamRepo: new StubMissionTeamRepo(existingTeam));

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.Equal(activity.Id, result.ActivityId);
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private static AssignTeamToActivityCommand BuildCommand(
        int activityId = 1, int missionId = 10, int rescueTeamId = 3) =>
        new(activityId, missionId, rescueTeamId, CoordinatorId);

    private static MissionActivityModel BuildActivity(int id = 1, int missionId = 10, int? sosRequestId = null) => new()
    {
        Id = id,
        MissionId = missionId,
        SosRequestId = sosRequestId,
        ActivityType = "DELIVER_SUPPLIES",
        Status = RESQ.Domain.Enum.Operations.MissionActivityStatus.Planned
    };

    private static AssignTeamToActivityCommandHandler BuildHandler(
        StubActivityRepo? activityRepo = null,
        StubMissionTeamRepo? missionTeamRepo = null,
        RecordingMediator? mediator = null)
    {
        return new AssignTeamToActivityCommandHandler(
            activityRepo ?? new StubActivityRepo(BuildActivity()),
            missionTeamRepo ?? new StubMissionTeamRepo(new MissionTeamModel { Id = 5, MissionId = 10, RescuerTeamId = 3 }),
            new StubSosRequestRepo(),
            new StubSosRequestUpdateRepo(),
            new StubTeamIncidentRepo(),
            mediator ?? new RecordingMediator(),
            new StubUnitOfWork(),
            NullLogger<AssignTeamToActivityCommandHandler>.Instance);
    }

    // ─── Stubs ────────────────────────────────────────────────────

    private sealed class StubActivityRepo(MissionActivityModel? activity) : IMissionActivityRepository
    {
        public int AssignCalls { get; private set; }
        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(activity);
        public Task<int> AddAsync(MissionActivityModel a, CancellationToken ct = default) => Task.FromResult(a.Id);
        public Task UpdateAsync(MissionActivityModel a, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int mid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionActivityModel>());
        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionActivityModel>());
        public Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int apId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MissionActivityModel>>([]);
        public Task UpdateStatusAsync(int aid, RESQ.Domain.Enum.Operations.MissionActivityStatus s, Guid db, string? img = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task AssignTeamAsync(int aid, int mtid, CancellationToken ct = default) { AssignCalls++; return Task.CompletedTask; }
        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> aids, Guid db, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubMissionTeamRepo(MissionTeamModel? team) : IMissionTeamRepository
    {
        public Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(team);
        public Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int mid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<int> CreateAsync(MissionTeamModel m, CancellationToken ct = default) => Task.FromResult(0);
        public Task UpdateStatusAsync(int id, string s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int id, string s, string? n, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateCurrentLocationAsync(int id, double lat, double lon, string src, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rtid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<MissionTeamModel?> GetByMissionAndTeamAsync(int mid, int rtid, CancellationToken ct = default) => Task.FromResult(team);
    }

    private sealed class StubSosRequestRepo : ISosRequestRepository
    {
        public Task CreateAsync(SosRequestModel sos, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(SosRequestModel sos, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<RESQ.Application.Common.Models.PagedResult<SosRequestModel>> GetAllPagedAsync(int pn, int ps, System.Collections.Generic.IReadOnlyCollection<RESQ.Domain.Enum.Emergency.SosRequestStatus>? statuses = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<SosRequestModel?>(null);
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int cid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task UpdateStatusAsync(int id, SosRequestStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusByClusterIdAsync(int cid, SosRequestStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
    }

    private sealed class StubSosRequestUpdateRepo : ISosRequestUpdateRepository
    {
        public Task AddVictimUpdateAsync(SosRequestVictimUpdateModel u, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> u, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(new Dictionary<int, IReadOnlyCollection<int>>());
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(new Dictionary<int, IReadOnlyCollection<int>>());
        public Task<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>> GetLatestVictimUpdatesBySosRequestIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>>(new Dictionary<int, SosRequestVictimUpdateModel>());
        public Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>>(new Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>());
    }

    private sealed class StubTeamIncidentRepo : ITeamIncidentRepository
    {
        public Task<IEnumerable<TeamIncidentModel>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<TeamIncidentModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<TeamIncidentModel?>(null);
        public Task<IEnumerable<TeamIncidentModel>> GetByMissionIdAsync(int mid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<IEnumerable<TeamIncidentModel>> GetByMissionTeamIdAsync(int mtid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<int> CreateAsync(TeamIncidentModel m, CancellationToken ct = default) => Task.FromResult(1);
        public Task UpdateStatusAsync(int id, RESQ.Domain.Enum.Operations.TeamIncidentStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateSupportSosRequestIdAsync(int id, int? sosId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
