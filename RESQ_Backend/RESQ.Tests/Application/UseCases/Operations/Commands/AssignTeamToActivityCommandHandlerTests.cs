using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToActivity;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToMission;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Entities.Personnel;
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

    [Fact]
    public async Task Handle_WithResolvedSosRequest_ReactivatesSosAndCluster()
    {
        var activity = BuildActivity(sosRequestId: 100);
        var existingTeam = new MissionTeamModel { Id = 5, MissionId = 10, RescuerTeamId = 3 };
        var sosRequest = new SosRequestModel
        {
            Id = 100,
            ClusterId = 20,
            Status = SosRequestStatus.Resolved
        };
        var cluster = new SosClusterModel
        {
            Id = 20,
            Status = SosClusterStatus.Completed,
            SosRequestIds = [100]
        };

        var handler = BuildHandler(
            activityRepo: new StubActivityRepo(activity),
            missionTeamRepo: new StubMissionTeamRepo(existingTeam),
            sosRequestRepo: new StubSosRequestRepo(sosRequest),
            sosClusterRepo: new StubSosClusterRepo(cluster));

        await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.Equal(SosRequestStatus.Assigned, sosRequest.Status);
        Assert.Equal(SosClusterStatus.InProgress, cluster.Status);
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
        RecordingMediator? mediator = null,
        StubSosRequestRepo? sosRequestRepo = null,
        StubSosClusterRepo? sosClusterRepo = null)
    {
        return new AssignTeamToActivityCommandHandler(
            activityRepo ?? new StubActivityRepo(BuildActivity()),
            missionTeamRepo ?? new StubMissionTeamRepo(new MissionTeamModel { Id = 5, MissionId = 10, RescuerTeamId = 3 }),
            new StubAssemblyPointRepo(),
            sosRequestRepo ?? new StubSosRequestRepo(),
            sosClusterRepo ?? new StubSosClusterRepo(),
            new StubSosRequestUpdateRepo(),
            new StubTeamIncidentRepo(),
            new StubOperationalHubService(),
            mediator ?? new RecordingMediator(),
            new StubUnitOfWork(),
            NullLogger<AssignTeamToActivityCommandHandler>.Instance);
    }

    // ─── Stubs ────────────────────────────────────────────────────

    private sealed class StubAssemblyPointRepo : IAssemblyPointRepository
    {
        public Task<AssemblyPointModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<AssemblyPointModel?>(null);
        public Task<AssemblyPointModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<AssemblyPointModel?>(null);
        public Task<AssemblyPointModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult<AssemblyPointModel?>(null);
        public Task CreateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PagedResult<AssemblyPointModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default, string? statusFilter = null) => Task.FromResult(new PagedResult<AssemblyPointModel>([], 0, pageNumber, pageSize));
        public Task<List<AssemblyPointModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<AssemblyPointModel>());
        public Task<Dictionary<int, List<RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById.AssemblyPointTeamDto>>> GetTeamsByAssemblyPointIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<int, List<RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById.AssemblyPointTeamDto>>());
        public Task<List<Guid>> GetAssignedRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default) => Task.FromResult(new List<Guid>());
        public Task<List<Guid>> GetTeamlessRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default) => Task.FromResult(new List<Guid>());
        public Task<bool> HasActiveTeamAsync(Guid rescuerUserId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task UpdateRescuerAssemblyPointAsync(Guid rescuerUserId, int? assemblyPointId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<Guid>> BulkUpdateRescuerAssemblyPointAsync(IReadOnlyList<Guid> userIds, int? assemblyPointId, CancellationToken cancellationToken = default) => Task.FromResult(new List<Guid>());
        public Task<List<Guid>> FilterUsersWithoutActiveTeamAsync(IReadOnlyList<Guid> userIds, CancellationToken cancellationToken = default) => Task.FromResult(new List<Guid>());
        public Task UnassignAllRescuersAsync(int assemblyPointId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubActivityRepo(MissionActivityModel? activity) : IMissionActivityRepository
    {
        public int AssignCalls { get; private set; }
        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(activity);
        public Task<int> AddAsync(MissionActivityModel a, CancellationToken ct = default) => Task.FromResult(a.Id);
        public Task UpdateAsync(MissionActivityModel a, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int mid, CancellationToken ct = default)
            => Task.FromResult(activity?.MissionId == mid
                ? new[] { activity }.AsEnumerable()
                : Enumerable.Empty<MissionActivityModel>());
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

    private sealed class StubSosRequestRepo(params SosRequestModel[] requests) : ISosRequestRepository
    {
        private readonly Dictionary<int, SosRequestModel> _requests = requests.ToDictionary(request => request.Id);

        public Task CreateAsync(SosRequestModel sos, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(SosRequestModel sos, CancellationToken ct = default)
        {
            _requests[sos.Id] = sos;
            return Task.CompletedTask;
        }
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<RESQ.Application.Common.Models.PagedResult<SosRequestModel>> GetAllPagedAsync(int pn, int ps, System.Collections.Generic.IReadOnlyCollection<RESQ.Domain.Enum.Emergency.SosRequestStatus>? statuses = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(_requests.GetValueOrDefault(id));
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int cid, CancellationToken ct = default)
            => Task.FromResult(_requests.Values.Where(request => request.ClusterId == cid).AsEnumerable());
        public Task UpdateStatusAsync(int id, SosRequestStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusByClusterIdAsync(int cid, SosRequestStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
    }

    private sealed class StubSosClusterRepo(params SosClusterModel[] clusters) : ISosClusterRepository
    {
        private readonly Dictionary<int, SosClusterModel> _clusters = clusters.ToDictionary(cluster => cluster.Id);

        public Task<SosClusterModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(_clusters.GetValueOrDefault(id));
        public Task<IEnumerable<SosClusterModel>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(_clusters.Values.AsEnumerable());
        public Task<int> CreateAsync(SosClusterModel cluster, CancellationToken ct = default)
        {
            _clusters[cluster.Id] = cluster;
            return Task.FromResult(cluster.Id);
        }
        public Task UpdateAsync(SosClusterModel cluster, CancellationToken ct = default)
        {
            _clusters[cluster.Id] = cluster;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(int id, CancellationToken ct = default)
        {
            _clusters.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class StubSosRequestUpdateRepo : ISosRequestUpdateRepository
    {
        public Task AddVictimUpdateAsync(SosRequestVictimUpdateModel u, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> u, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(new Dictionary<int, IReadOnlyCollection<int>>());
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(new Dictionary<int, IReadOnlyCollection<int>>());
        public Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>>(new Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>());
    }

    private sealed class StubTeamIncidentRepo : ITeamIncidentRepository
    {
        public Task<IEnumerable<TeamIncidentModel>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<RESQ.Application.Common.Models.PagedResult<TeamIncidentModel>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RESQ.Application.Common.Models.PagedResult<TeamIncidentModel>> GetPagedByMissionIdAsync(int missionId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TeamIncidentModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<TeamIncidentModel?>(null);
        public Task<IEnumerable<TeamIncidentModel>> GetByMissionIdAsync(int mid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<IEnumerable<TeamIncidentModel>> GetByMissionTeamIdAsync(int mtid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<int> CreateAsync(TeamIncidentModel m, CancellationToken ct = default) => Task.FromResult(1);
        public Task UpdateStatusAsync(int id, RESQ.Domain.Enum.Operations.TeamIncidentStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateSupportSosRequestIdAsync(int id, int? sosId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
