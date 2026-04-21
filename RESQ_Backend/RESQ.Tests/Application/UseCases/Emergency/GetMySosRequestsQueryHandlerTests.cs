using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.UseCases.Emergency.Queries.GetMySosRequests;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class GetMySosRequestsQueryHandlerTests
{
    private static readonly GeoLocation HcmLocation = new(10.762622, 106.660172);
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    // ─── Merge owner + companion, deduplicate ─────────────────────

    [Fact]
    public async Task Handle_MergesOwnerAndCompanionRequests_Deduplicated()
    {
        var sos1 = BuildSos(1, UserId, SosRequestStatus.Pending);
        var sos2 = BuildSos(2, Guid.NewGuid(), SosRequestStatus.InProgress); // companion SOS
        var sos1Dup = BuildSos(1, UserId, SosRequestStatus.Pending); // same ID, appears as companion too

        var handler = BuildHandler(
            ownRequests: [sos1],
            companionRequests: [sos2, sos1Dup]);

        var result = await handler.Handle(new GetMySosRequestsQuery(UserId), CancellationToken.None);

        Assert.Equal(2, result.SosRequests.Count);
        Assert.DoesNotContain(result.SosRequests, s => result.SosRequests.Count(x => x.Id == s.Id) > 1);
    }

    // ─── Sort by CreatedAt desc ───────────────────────────────────

    [Fact]
    public async Task Handle_SortsByCreatedAtDescending()
    {
        var older = BuildSos(1, UserId);
        older.CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = BuildSos(2, UserId);
        newer.CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

        var handler = BuildHandler(ownRequests: [older, newer]);

        var result = await handler.Handle(new GetMySosRequestsQuery(UserId), CancellationToken.None);

        Assert.Equal(2, result.SosRequests[0].Id); // newer first
        Assert.Equal(1, result.SosRequests[1].Id);
    }

    // ─── IsCompanion is set correctly ─────────────────────────────

    [Fact]
    public async Task Handle_SetsIsCompanionCorrectly()
    {
        var ownSos = BuildSos(1, UserId);
        var companionSos = BuildSos(2, Guid.NewGuid());

        var handler = BuildHandler(
            ownRequests: [ownSos],
            companionRequests: [companionSos]);

        var result = await handler.Handle(new GetMySosRequestsQuery(UserId), CancellationToken.None);

        var own = result.SosRequests.First(s => s.Id == 1);
        var comp = result.SosRequests.First(s => s.Id == 2);
        Assert.False(own.IsCompanion);
        Assert.True(comp.IsCompanion);
    }

    // ─── Apply latest victim update ───────────────────────────────

    [Fact]
    public async Task Handle_AppliesLatestVictimUpdate()
    {
        var sos = BuildSos(1, UserId);
        var victimUpdate = new SosRequestVictimUpdateModel
        {
            Id = 10,
            SosRequestId = 1,
            RawMessage = "Updated message",
            SosType = "EVACUATION",
            UpdatedAt = DateTime.UtcNow,
            UpdatedByUserId = UserId
        };
        var updateRepo = new StubSosRequestUpdateRepository(
            victimUpdates: new Dictionary<int, SosRequestVictimUpdateModel> { [1] = victimUpdate });

        var handler = BuildHandler(
            ownRequests: [sos],
            updateRepo: updateRepo);

        var result = await handler.Handle(new GetMySosRequestsQuery(UserId), CancellationToken.None);

        var dto = Assert.Single(result.SosRequests);
        Assert.Equal("Updated message", dto.RawMessage);
        Assert.Equal("EVACUATION", dto.SosType);
    }

    // ─── Apply latest incident note ───────────────────────────────

    [Fact]
    public async Task Handle_AppliesLatestIncidentNote()
    {
        var sos = BuildSos(1, UserId);
        var incidents = new List<SosRequestIncidentUpdateModel>
        {
            new() { Id = 100, SosRequestId = 1, Note = "Đội gặp sự cố tại hiện trường", CreatedAt = DateTime.UtcNow }
        };
        var updateRepo = new StubSosRequestUpdateRepository(
            incidentHistory: new Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>
            {
                [1] = incidents
            });

        var handler = BuildHandler(
            ownRequests: [sos],
            updateRepo: updateRepo);

        var result = await handler.Handle(new GetMySosRequestsQuery(UserId), CancellationToken.None);

        var dto = Assert.Single(result.SosRequests);
        Assert.Equal("Đội gặp sự cố tại hiện trường", dto.LatestIncidentNote);
    }

    // ─── Empty results ────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoSosRequests()
    {
        var handler = BuildHandler();

        var result = await handler.Handle(new GetMySosRequestsQuery(UserId), CancellationToken.None);

        Assert.Empty(result.SosRequests);
    }

    // ─── Dedup prefers own over companion ─────────────────────────

    [Fact]
    public async Task Handle_DeduplicatesPrefersOwnOverCompanion()
    {
        var sos = BuildSos(1, UserId);

        var handler = BuildHandler(
            ownRequests: [sos],
            companionRequests: [sos]);

        var result = await handler.Handle(new GetMySosRequestsQuery(UserId), CancellationToken.None);

        var dto = Assert.Single(result.SosRequests);
        Assert.False(dto.IsCompanion); // own appears first in concat, so GroupBy picks the own one
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private static SosRequestModel BuildSos(int id, Guid userId,
        SosRequestStatus status = SosRequestStatus.Pending)
    {
        var sos = SosRequestModel.Create(userId, HcmLocation, "Cần cứu trợ");
        sos.Id = id;
        sos.Status = status;
        return sos;
    }

    private static GetMySosRequestsQueryHandler BuildHandler(
        List<SosRequestModel>? ownRequests = null,
        List<SosRequestModel>? companionRequests = null,
        StubSosRequestUpdateRepository? updateRepo = null)
    {
        return new GetMySosRequestsQueryHandler(
            new StubSosRequestRepository(ownRequests ?? [], companionRequests ?? []),
            new StubSosRequestCompanionRepository(),
            updateRepo ?? new StubSosRequestUpdateRepository(),
            NullLogger<GetMySosRequestsQueryHandler>.Instance);
    }

    // ─── Stubs ────────────────────────────────────────────────────

    private sealed class StubSosRequestRepository(
        List<SosRequestModel> ownRequests,
        List<SosRequestModel>? companionRequests = null) : ISosRequestRepository
    {
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IEnumerable<SosRequestModel>>(ownRequests);
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IEnumerable<SosRequestModel>>(companionRequests ?? []);
        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<SosRequestModel?>(null);
        public Task CreateAsync(SosRequestModel m, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(SosRequestModel m, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<PagedResult<SosRequestModel>> GetAllPagedAsync(int pn, int ps, System.Collections.Generic.IReadOnlyCollection<RESQ.Domain.Enum.Emergency.SosRequestStatus>? statuses = null, CancellationToken ct = default)
            => Task.FromResult(new PagedResult<SosRequestModel>([], 0, pn, ps));
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int cid, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task UpdateStatusAsync(int id, SosRequestStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusByClusterIdAsync(int cid, SosRequestStatus s, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubSosRequestCompanionRepository : ISosRequestCompanionRepository
    {
        public Task<bool> IsCompanionAsync(int sosRequestId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task CreateRangeAsync(IEnumerable<SosRequestCompanionRecord> c, CancellationToken ct = default) => Task.CompletedTask;
        public Task<List<SosRequestCompanionRecord>> GetBySosRequestIdAsync(int sid, CancellationToken ct = default)
            => Task.FromResult(new List<SosRequestCompanionRecord>());
        public Task<List<int>> GetSosRequestIdsByUserIdAsync(Guid uid, CancellationToken ct = default)
            => Task.FromResult(new List<int>());
    }

    private sealed class StubSosRequestUpdateRepository(
        Dictionary<int, SosRequestVictimUpdateModel>? victimUpdates = null,
        Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>? incidentHistory = null)
        : ISosRequestUpdateRepository
    {
        public Task AddVictimUpdateAsync(SosRequestVictimUpdateModel u, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> u, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(
            IEnumerable<int> ids, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(
                new Dictionary<int, IReadOnlyCollection<int>>());
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(
            IEnumerable<int> ids, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(
                new Dictionary<int, IReadOnlyCollection<int>>());
        public Task<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>> GetLatestVictimUpdatesBySosRequestIdsAsync(
            IEnumerable<int> ids, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>>(
                victimUpdates ?? new Dictionary<int, SosRequestVictimUpdateModel>());
        public Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(
            IEnumerable<int> ids, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>>(
                incidentHistory ?? new Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>());
    }
}
