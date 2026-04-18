using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Identity;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Identity;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Emergency;

/// <summary>
/// FE-01 – SOS Request &amp; Victim Interaction: Create SOS Request handler tests.
/// Covers: Create with GPS coords, Categorize SOS Priority, Specify Emergency Needs, AI queue.
/// </summary>
public class CreateSosRequestCommandHandlerTests
{
    private static readonly Guid ValidUserId = Guid.NewGuid();
    private static readonly GeoLocation HcmLocation = new(10.762622, 106.660172);

    // ── FE-01: Create SOS Request with Real-time GPS Coordinates ──

    [Fact]
    public async Task Handle_ThrowsNotFoundException_WhenUserDoesNotExist()
    {
        var handler = BuildHandler(userRepo: new StubUserRepo(null));

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(BuildCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CreatesSosRequest_AndEvaluatesPriority()
    {
        var unitOfWork = new StubUnitOfWork();
        var handler = BuildHandler(unitOfWork: unitOfWork,
            evalService: new StubEvalService(SosPriorityLevel.High, 75.0));

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.Equal("Pending", result.Status);
        Assert.Equal("High", result.PriorityLevel);
        Assert.Equal(ValidUserId, result.UserId);
        Assert.True(unitOfWork.SaveCalls >= 2);
    }

    [Fact]
    public async Task Handle_QueuesAiAnalysis_AfterCreation()
    {
        var aiQueue = new StubAiQueue();
        var handler = BuildHandler(aiQueue: aiQueue);

        await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.Single(aiQueue.QueuedTasks);
    }

    [Fact]
    public async Task Handle_PreservesGpsCoordinates_InResponse()
    {
        var handler = BuildHandler();
        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.Equal(10.762622, result.Latitude);
        Assert.Equal(106.660172, result.Longitude);
    }

    // ── FE-01: Categorize SOS Priority Levels (P1-Critical through P4-Low) ──

    [Theory]
    [InlineData(SosPriorityLevel.Critical, 95.0)]
    [InlineData(SosPriorityLevel.High, 75.0)]
    [InlineData(SosPriorityLevel.Medium, 50.0)]
    [InlineData(SosPriorityLevel.Low, 20.0)]
    public async Task Handle_AssignsPriorityLevel_FromEvaluationService(SosPriorityLevel expected, double score)
    {
        var handler = BuildHandler(evalService: new StubEvalService(expected, score));
        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.Equal(expected.ToString(), result.PriorityLevel);
    }

    // ── FE-01: Specify Emergency Needs (structured data) ──

    [Fact]
    public async Task Handle_AcceptsStructuredData_WhenProvided()
    {
        var handler = BuildHandler(evalService: new StubEvalService(SosPriorityLevel.High, 70.0));
        var cmd = BuildCommand(structuredData: """{"incident":{"situation":"flood"},"victims":[]}""");

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ValidUserId, result.UserId);
    }

    // ── FE-03: AI Confidence Scoring ──

    [Fact]
    public async Task Handle_SavesRuleEvaluation_WithScore()
    {
        var evalRepo = new StubEvalRepo();
        var handler = BuildHandler(evalRepo: evalRepo, evalService: new StubEvalService(SosPriorityLevel.High, 82.5));

        await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.NotNull(evalRepo.LastCreated);
        Assert.Equal(82.5, evalRepo.LastCreated!.TotalScore);
    }

    // ── Builder ──

    private static CreateSosRequestCommand BuildCommand(
        Guid? userId = null, string rawMessage = "Cần cứu trợ khẩn cấp",
        string? structuredData = null, string? sosType = null)
        => new(userId ?? ValidUserId, HcmLocation, rawMessage,
               StructuredData: structuredData, SosType: sosType);

    private static CreateSosRequestCommandHandler BuildHandler(
        StubSosRepo? sosRepo = null, StubEvalRepo? evalRepo = null,
        StubEvalService? evalService = null, StubAiQueue? aiQueue = null,
        StubUserRepo? userRepo = null, StubUnitOfWork? unitOfWork = null)
        => new(
            sosRepo ?? new StubSosRepo(),
            evalRepo ?? new StubEvalRepo(),
            evalService ?? new StubEvalService(SosPriorityLevel.Medium, 50.0),
            aiQueue ?? new StubAiQueue(),
            userRepo ?? new StubUserRepo(new UserModel { Id = ValidUserId, RoleId = 2 }),
            new StubCompanionRepo(),
            new StubFirebase(),
            unitOfWork ?? new StubUnitOfWork(),
            new StubDashboard(),
            NullLogger<CreateSosRequestCommandHandler>.Instance);

    // ── Stubs ──

    private sealed class StubUserRepo(UserModel? user) : IUserRepository
    {
        public Task<UserModel?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(user);
        public Task<UserModel?> GetByUsernameAsync(string u, CancellationToken ct = default) => Task.FromResult<UserModel?>(null);
        public Task<UserModel?> GetByEmailAsync(string e, CancellationToken ct = default) => Task.FromResult<UserModel?>(null);
        public Task<UserModel?> GetByPhoneAsync(string p, CancellationToken ct = default) => Task.FromResult<UserModel?>(null);
        public Task<List<UserModel>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default) => Task.FromResult(new List<UserModel>());
        public Task<UserModel?> GetByEmailVerificationTokenAsync(string t, CancellationToken ct = default) => Task.FromResult<UserModel?>(null);
        public Task<UserModel?> GetByPasswordResetTokenAsync(string t, CancellationToken ct = default) => Task.FromResult<UserModel?>(null);
        public Task CreateAsync(UserModel u, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(UserModel u, CancellationToken ct = default) => Task.CompletedTask;
        public Task<PagedResult<UserModel>> GetPagedAsync(int pn, int ps, int? roleId = null, bool? isBanned = null, string? search = null, int? excludeRoleId = null, bool? isEligible = null, RescuerType? rescuerType = null, CancellationToken ct = default) => Task.FromResult(new PagedResult<UserModel>([], 0, pn, ps));
        public Task<PagedResult<UserModel>> GetPagedForPermissionAsync(int pn, int ps, int? roleId = null, string? search = null, CancellationToken ct = default) => Task.FromResult(new PagedResult<UserModel>([], 0, pn, ps));
        public Task<List<Guid>> GetActiveAdminUserIdsAsync(CancellationToken ct = default) => Task.FromResult(new List<Guid>());
        public Task<List<Guid>> GetActiveCoordinatorUserIdsAsync(CancellationToken ct = default) => Task.FromResult(new List<Guid>());
        public Task<List<AvailableManagerDto>> GetAvailableManagersAsync(int? excludeDepotId = null, CancellationToken ct = default) => Task.FromResult(new List<AvailableManagerDto>());
    }

    private sealed class StubSosRepo : ISosRequestRepository
    {
        private readonly List<SosRequestModel> _store = [];
        public Task CreateAsync(SosRequestModel sos, CancellationToken ct = default) { sos.Id = _store.Count + 1; _store.Add(sos); return Task.CompletedTask; }
        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(_store.FirstOrDefault(s => s.Id == id));
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) => Task.FromResult<IEnumerable<SosRequestModel>>(_store.Where(s => s.UserId == userId));
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IEnumerable<SosRequestModel>>(_store);
        public Task<PagedResult<SosRequestModel>> GetAllPagedAsync(int pn, int ps, CancellationToken ct = default) => Task.FromResult(new PagedResult<SosRequestModel>(_store, _store.Count, pn, ps));
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int cid, CancellationToken ct = default) => Task.FromResult<IEnumerable<SosRequestModel>>([]);
        public Task UpdateAsync(SosRequestModel sos, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int id, SosRequestStatus status, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusByClusterIdAsync(int cid, SosRequestStatus status, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken ct = default) => Task.FromResult<IEnumerable<SosRequestModel>>([]);
    }

    private sealed class StubEvalRepo : ISosRuleEvaluationRepository
    {
        public SosRuleEvaluationModel? LastCreated { get; private set; }
        public Task CreateAsync(SosRuleEvaluationModel e, CancellationToken ct = default) { LastCreated = e; return Task.CompletedTask; }
        public Task<SosRuleEvaluationModel?> GetBySosRequestIdAsync(int id, CancellationToken ct = default) => Task.FromResult<SosRuleEvaluationModel?>(null);
    }

    private sealed class StubEvalService(SosPriorityLevel level, double score) : ISosPriorityEvaluationService
    {
        public Task<SosRuleEvaluationModel> EvaluateAsync(int sosReqId, string? json, string? sosType, CancellationToken ct = default)
            => Task.FromResult(new SosRuleEvaluationModel { SosRequestId = sosReqId, PriorityLevel = level, TotalScore = score });
        public Task<SosRuleEvaluationModel> EvaluateWithConfigAsync(int sosReqId, string? json, string? sosType, RESQ.Domain.Entities.System.SosPriorityRuleConfigModel? cfg, CancellationToken ct = default)
            => EvaluateAsync(sosReqId, json, sosType, ct);
    }

    private sealed class StubAiQueue : ISosAiAnalysisQueue
    {
        public List<SosAiAnalysisTask> QueuedTasks { get; } = [];
        public ValueTask QueueAsync(SosAiAnalysisTask task) { QueuedTasks.Add(task); return ValueTask.CompletedTask; }
    }

    private sealed class StubCompanionRepo : ISosRequestCompanionRepository
    {
        public Task CreateRangeAsync(IEnumerable<SosRequestCompanionRecord> c, CancellationToken ct = default) => Task.CompletedTask;
        public Task<List<SosRequestCompanionRecord>> GetBySosRequestIdAsync(int id, CancellationToken ct = default) => Task.FromResult(new List<SosRequestCompanionRecord>());
        public Task<List<int>> GetSosRequestIdsByUserIdAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(new List<int>());
        public Task<bool> IsCompanionAsync(int sosRequestId, Guid userId, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class StubFirebase : IFirebaseService
    {
        public Task<FirebasePhoneTokenInfo> VerifyIdTokenAsync(string t, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<FirebaseGoogleUserInfo> VerifyGoogleIdTokenAsync(string t, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SendNotificationToUserAsync(Guid u, string ti, string bo, string ty, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendNotificationToUserAsync(Guid u, string ti, string bo, string ty, Dictionary<string, string> d, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendToTopicAsync(string tp, string ti, string bo, string ty, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendToTopicAsync(string tp, string ti, string bo, Dictionary<string, string> d, CancellationToken ct = default) => Task.CompletedTask;
        public Task SubscribeToUserTopicAsync(string fc, Guid u, CancellationToken ct = default) => Task.CompletedTask;
        public Task UnsubscribeFromUserTopicAsync(string fc, Guid u, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubDashboard : IDashboardHubService
    {
        public Task PushVictimsByPeriodAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task PushAssemblyPointSnapshotAsync(int apId, string op, CancellationToken ct = default) => Task.CompletedTask;
    }
}
