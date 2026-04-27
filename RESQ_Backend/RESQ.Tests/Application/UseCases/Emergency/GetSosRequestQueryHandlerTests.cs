using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Emergency.Queries.GetSosEvaluation;
using RESQ.Application.UseCases.Emergency.Queries.GetSosRequests;
using RESQ.Application.UseCases.Emergency.Shared;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Identity;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class GetSosRequestQueryHandlerTests
{
    private static readonly GeoLocation HcmLocation = new(10.762622, 106.660172);
    private static readonly Guid OwnerId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid CompanionId = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid CoordinatorId = Guid.Parse("dddddddd-0000-0000-0000-000000000004");
    private static readonly Guid StrangerId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    // ─── Owner can view own SOS ───────────────────────────────────

    [Fact]
    public async Task Handle_OwnerCanViewOwnSos()
    {
        var sos = BuildSos(1, OwnerId);
        var handler = BuildHandler(sosRepo: new StubSosRequestRepository(sos));

        var result = await handler.Handle(
            new GetSosRequestQuery(1, OwnerId, HasPrivilegedAccess: false),
            CancellationToken.None);

        Assert.Equal(1, result.SosRequest.Id);
        Assert.Equal(OwnerId, result.SosRequest.UserId);
    }

    // ─── Companion can view SOS ───────────────────────────────────

    [Fact]
    public async Task Handle_CompanionCanViewSos()
    {
        var sos = BuildSos(1, OwnerId);
        var handler = BuildHandler(
            sosRepo: new StubSosRequestRepository(sos),
            companionRepo: new StubSosRequestCompanionRepository(isCompanion: true));

        var result = await handler.Handle(
            new GetSosRequestQuery(1, CompanionId, HasPrivilegedAccess: false),
            CancellationToken.None);

        Assert.Equal(1, result.SosRequest.Id);
    }

    // ─── Coordinator / privileged can view any SOS ────────────────

    [Fact]
    public async Task Handle_PrivilegedUserCanViewAnySos()
    {
        var sos = BuildSos(1, OwnerId);
        var handler = BuildHandler(sosRepo: new StubSosRequestRepository(sos));

        var result = await handler.Handle(
            new GetSosRequestQuery(1, CoordinatorId, HasPrivilegedAccess: true),
            CancellationToken.None);

        Assert.Equal(1, result.SosRequest.Id);
    }

    // ─── Stranger gets Forbidden ──────────────────────────────────

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenStrangerWithoutAccess()
    {
        var sos = BuildSos(1, OwnerId);
        var handler = BuildHandler(
            sosRepo: new StubSosRequestRepository(sos),
            companionRepo: new StubSosRequestCompanionRepository(isCompanion: false));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(
                new GetSosRequestQuery(1, StrangerId, HasPrivilegedAccess: false),
                CancellationToken.None));
    }

    // ─── SOS not found throws NotFound ────────────────────────────

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenSosDoesNotExist()
    {
        var handler = BuildHandler(sosRepo: new StubSosRequestRepository(null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new GetSosRequestQuery(999, OwnerId, HasPrivilegedAccess: false),
                CancellationToken.None));
    }

    // ─── Apply latest victim update ───────────────────────────────

    [Fact]
    public async Task Handle_AppliesLatestVictimUpdate()
    {
        var sos = BuildSos(1, OwnerId);
        var victimUpdate = new SosRequestVictimUpdateModel
        {
            Id = 10,
            SosRequestId = 1,
            RawMessage = "Updated message from victim",
            SosType = "MEDICAL",
            UpdatedAt = DateTime.UtcNow,
            UpdatedByUserId = OwnerId
        };
        var updateRepo = new StubSosRequestUpdateRepository(
            victimUpdates: new Dictionary<int, SosRequestVictimUpdateModel> { [1] = victimUpdate });

        var handler = BuildHandler(
            sosRepo: new StubSosRequestRepository(sos),
            updateRepo: updateRepo);

        var result = await handler.Handle(
            new GetSosRequestQuery(1, OwnerId, HasPrivilegedAccess: false),
            CancellationToken.None);

        Assert.Equal("Updated message from victim", result.SosRequest.RawMessage);
        Assert.Equal("MEDICAL", result.SosRequest.SosType);
    }

    // ─── Incident history is returned ─────────────────────────────

    [Fact]
    public async Task Handle_ReturnsIncidentHistory()
    {
        var sos = BuildSos(1, OwnerId);
        var incidents = new List<SosRequestIncidentUpdateModel>
        {
            new() { Id = 100, SosRequestId = 1, Note = "Đội gặp sự cố", CreatedAt = DateTime.UtcNow, TeamName = "Team Alpha" },
            new() { Id = 101, SosRequestId = 1, Note = "Đã xử lý", CreatedAt = DateTime.UtcNow.AddMinutes(-5) }
        };
        var updateRepo = new StubSosRequestUpdateRepository(
            incidentHistory: new Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>
            {
                [1] = incidents
            });

        var handler = BuildHandler(
            sosRepo: new StubSosRequestRepository(sos),
            updateRepo: updateRepo);

        var result = await handler.Handle(
            new GetSosRequestQuery(1, OwnerId, HasPrivilegedAccess: false),
            CancellationToken.None);

        Assert.NotNull(result.SosRequest.IncidentHistory);
        Assert.Equal(2, result.SosRequest.IncidentHistory!.Count);
        Assert.Equal("Đội gặp sự cố", result.SosRequest.LatestIncidentNote);
    }

    // ─── Companion list is returned ───────────────────────────────

    [Fact]
    public async Task Handle_ReturnsCompanionList()
    {
        var sos = BuildSos(1, OwnerId);
        var companions = new List<SosRequestCompanionRecord>
        {
            new(1, 1, CompanionId, "0909123456", DateTime.UtcNow)
        };
        var users = new List<UserModel>
        {
            new() { Id = CompanionId, FirstName = "Minh", LastName = "Nguyễn", Phone = "0909123456" }
        };
        var companionRepo = new StubSosRequestCompanionRepository(isCompanion: false, companions: companions);
        var userRepo = new StubUserRepository(users);

        var handler = BuildHandler(
            sosRepo: new StubSosRequestRepository(sos),
            companionRepo: companionRepo,
            userRepo: userRepo);

        var result = await handler.Handle(
            new GetSosRequestQuery(1, OwnerId, HasPrivilegedAccess: false),
            CancellationToken.None);

        Assert.NotNull(result.SosRequest.Companions);
        Assert.Single(result.SosRequest.Companions!);
        Assert.Equal(CompanionId, result.SosRequest.Companions![0].UserId);
    }

    // ─── Invalid JSON in structured data does not crash ───────────

    [Fact]
    public async Task Handle_ReturnsLatestSlimAiAnalysisWithoutDebugPayload()
    {
        var sos = BuildSos(1, OwnerId);
        var older = SosAiAnalysisModel.Create(
            1,
            "gemini-old",
            "v3.1",
            "PRIORITY_ANALYSIS",
            "Moderate",
            "Medium",
            30,
            true,
            "Phân tích cũ.");
        older.Id = 10;
        older.CreatedAt = DateTime.UtcNow.AddMinutes(-10);

        var latest = SosAiAnalysisModel.Create(
            1,
            "gemini-new",
            "v3.2",
            "PRIORITY_ANALYSIS",
            "Critical",
            "Critical",
            82,
            false,
            "Nạn nhân bất tỉnh và cần sơ tán ngay. AI suggested score 82. AI does not agree with the current rule-base score 35 (Medium) and sees a different urgency level.",
            metadata: """
                {
                  "rawResponse": "{}",
                  "analysisResult": {
                    "suggested_priority": "Critical",
                    "suggested_severity_level": "Critical",
                    "needs_immediate_safe_transfer": true,
                    "can_wait_for_combined_mission": false,
                    "handling_reason": "Nạn nhân bất tỉnh nên không thể chờ ghép mission. AI suggested score 82."
                  },
                  "ruleBaseContext": {},
                  "ruleConfigContext": {}
                }
                """);
        latest.Id = 11;
        latest.CreatedAt = DateTime.UtcNow;

        var handler = BuildHandler(
            sosRepo: new StubSosRequestRepository(sos),
            aiRepo: new StubSosAiAnalysisRepository([older, latest]));

        var result = await handler.Handle(
            new GetSosRequestQuery(1, OwnerId, HasPrivilegedAccess: false),
            CancellationToken.None);

        var aiAnalysis = Assert.IsType<SosRequestDetailAiAnalysisDto>(result.SosRequest.Evaluation.AiAnalysis);
        Assert.True(result.SosRequest.Evaluation.HasAiAnalysis);
        Assert.Equal(11, aiAnalysis.Id);
        Assert.Equal("Critical", aiAnalysis.SuggestedPriority);
        Assert.Equal(82, aiAnalysis.SuggestedPriorityScore);
        Assert.DoesNotContain("AI suggested", aiAnalysis.Explanation);
        Assert.DoesNotContain("rule-base score", aiAnalysis.Explanation);
        Assert.DoesNotContain("AI suggested", aiAnalysis.HandlingReason);

        var json = JsonSerializer.Serialize(
            result,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.DoesNotContain("\"metadata\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rawResponse", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ruleBaseContext", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ruleConfigContext", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("modelName", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("modelVersion", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("analysisType", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("suggestionScope", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("adoptedAt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aiAnalyses", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_InvalidJson_DoesNotFail()
    {
        var sos = BuildSos(1, OwnerId);
        sos.StructuredData = "not valid json {{{";
        sos.NetworkMetadata = "broken}}";
        sos.SenderInfo = null;

        var handler = BuildHandler(sosRepo: new StubSosRequestRepository(sos));

        var result = await handler.Handle(
            new GetSosRequestQuery(1, OwnerId, HasPrivilegedAccess: false),
            CancellationToken.None);

        Assert.Equal(1, result.SosRequest.Id);
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private static SosRequestModel BuildSos(int id, Guid userId,
        SosRequestStatus status = SosRequestStatus.Pending)
    {
        var sos = SosRequestModel.Create(userId, HcmLocation, "Cần cứu trợ khẩn cấp");
        sos.Id = id;
        sos.Status = status;
        return sos;
    }

    private static GetSosRequestQueryHandler BuildHandler(
        StubSosRequestRepository? sosRepo = null,
        StubSosRequestCompanionRepository? companionRepo = null,
        StubSosRequestUpdateRepository? updateRepo = null,
        StubSosRuleEvaluationRepository? ruleRepo = null,
        StubSosAiAnalysisRepository? aiRepo = null,
        StubUserRepository? userRepo = null)
    {
        var resolvedSosRepo = sosRepo ?? new StubSosRequestRepository(null);
        var resolvedCompanionRepo = companionRepo ?? new StubSosRequestCompanionRepository(isCompanion: false);
        var snapshotBuilder = new SosRequestSnapshotBuilder(
            resolvedSosRepo,
            resolvedCompanionRepo,
            updateRepo ?? new StubSosRequestUpdateRepository(),
            ruleRepo ?? new StubSosRuleEvaluationRepository(null),
            aiRepo ?? new StubSosAiAnalysisRepository([]),
            userRepo ?? new StubUserRepository([]));

        return new GetSosRequestQueryHandler(
            resolvedSosRepo,
            resolvedCompanionRepo,
            snapshotBuilder,
            NullLogger<GetSosRequestQueryHandler>.Instance);
    }

    // ─── Stubs ────────────────────────────────────────────────────

    private sealed class StubSosRequestRepository(SosRequestModel? sos) : ISosRequestRepository
    {
        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(sos);
        public Task CreateAsync(SosRequestModel m, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(SosRequestModel m, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<PagedResult<SosRequestModel>> GetAllPagedAsync(int pn, int ps, System.Collections.Generic.IReadOnlyCollection<RESQ.Domain.Enum.Emergency.SosRequestStatus>? statuses = null, CancellationToken ct = default)
            => Task.FromResult(new PagedResult<SosRequestModel>([], 0, pn, ps));
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int cid, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task UpdateStatusAsync(int id, SosRequestStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusByClusterIdAsync(int cid, SosRequestStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<SosRequestModel>());
    }

    private sealed class StubSosRequestCompanionRepository(
        bool isCompanion,
        List<SosRequestCompanionRecord>? companions = null) : ISosRequestCompanionRepository
    {
        public Task<bool> IsCompanionAsync(int sosRequestId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(isCompanion);
        public Task CreateRangeAsync(IEnumerable<SosRequestCompanionRecord> c, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<List<SosRequestCompanionRecord>> GetBySosRequestIdAsync(int sosRequestId, CancellationToken ct = default)
            => Task.FromResult(companions ?? []);
        public Task<List<int>> GetSosRequestIdsByUserIdAsync(Guid userId, CancellationToken ct = default)
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

    private sealed class StubUserRepository(List<UserModel> users) : IUserRepository
    {
        public Task<List<UserModel>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
            => Task.FromResult(users.Where(u => ids.Contains(u.Id)).ToList());
        public Task<UserModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(users.FirstOrDefault(u => u.Id == id));
        public Task<UserModel?> GetByUsernameAsync(string u, CancellationToken ct = default) => Task.FromResult<UserModel?>(null);
        public Task<UserModel?> GetByEmailAsync(string e, CancellationToken ct = default) => Task.FromResult<UserModel?>(null);
        public Task<UserModel?> GetByPhoneAsync(string p, CancellationToken ct = default) => Task.FromResult<UserModel?>(null);
        public Task<UserModel?> GetByEmailVerificationTokenAsync(string t, CancellationToken ct = default) => Task.FromResult<UserModel?>(null);
        public Task<UserModel?> GetByPasswordResetTokenAsync(string t, CancellationToken ct = default) => Task.FromResult<UserModel?>(null);
        public Task CreateAsync(UserModel u, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(UserModel u, CancellationToken ct = default) => Task.CompletedTask;
        public Task<PagedResult<UserModel>> GetPagedAsync(int pn, int ps, int? r = null, bool? b = null,
            string? s = null, int? er = null, bool? ie = null, RESQ.Domain.Enum.Identity.RescuerType? rt = null, CancellationToken ct = default)
            => Task.FromResult(new PagedResult<UserModel>([], 0, pn, ps));
        public Task<PagedResult<UserModel>> GetPagedForPermissionAsync(int pn, int ps, int? r = null,
            string? n = null, string? p = null, string? e = null, CancellationToken ct = default)
            => Task.FromResult(new PagedResult<UserModel>([], 0, pn, ps));
        public Task<List<Guid>> GetActiveAdminUserIdsAsync(CancellationToken ct = default) => Task.FromResult(new List<Guid>());
        public Task<List<Guid>> GetActiveCoordinatorUserIdsAsync(CancellationToken ct = default) => Task.FromResult(new List<Guid>());
        public Task<List<AvailableManagerDto>> GetAvailableManagersAsync(int? excludeDepotId = null, CancellationToken ct = default) => Task.FromResult(new List<AvailableManagerDto>());
    }

    private sealed class StubSosRuleEvaluationRepository(SosRuleEvaluationModel? evaluation) : ISosRuleEvaluationRepository
    {
        public Task CreateAsync(SosRuleEvaluationModel evaluation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<SosRuleEvaluationModel?> GetBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default)
            => Task.FromResult(evaluation);
    }

    private sealed class StubSosAiAnalysisRepository(List<SosAiAnalysisModel> analyses) : ISosAiAnalysisRepository
    {
        public Task CreateAsync(SosAiAnalysisModel analysis, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<SosAiAnalysisModel?> GetBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default)
            => Task.FromResult(analyses.FirstOrDefault());
        public Task<IEnumerable<SosAiAnalysisModel>> GetAllBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<SosAiAnalysisModel>>(analyses);
        public Task<IReadOnlyDictionary<int, SosAiAnalysisModel>> GetLatestBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, SosAiAnalysisModel>>(new Dictionary<int, SosAiAnalysisModel>());
    }
}
