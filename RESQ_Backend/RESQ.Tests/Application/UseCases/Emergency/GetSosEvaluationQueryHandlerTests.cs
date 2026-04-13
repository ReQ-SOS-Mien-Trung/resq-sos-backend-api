using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.UseCases.Emergency.Queries.GetSosEvaluation;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class GetSosEvaluationQueryHandlerTests
{
    private static readonly GeoLocation HcmLocation = new(10.762622, 106.660172);
    private static readonly Guid OwnerId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid CompanionId = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid CoordinatorId = Guid.Parse("dddddddd-0000-0000-0000-000000000004");
    private static readonly Guid StrangerId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    // ─── Owner can view evaluation ────────────────────────────────

    [Fact]
    public async Task Handle_OwnerCanViewEvaluation()
    {
        var sos = BuildSos(1, OwnerId);
        var ruleEval = BuildRuleEvaluation(1, SosPriorityLevel.High, 75.0);
        var handler = BuildHandler(
            sosRepo: new StubSosRequestRepository(sos),
            ruleRepo: new StubRuleEvaluationRepository(ruleEval));

        var result = await handler.Handle(
            new GetSosEvaluationQuery(1, OwnerId, HasPrivilegedAccess: false),
            CancellationToken.None);

        Assert.Equal(1, result.SosRequestId);
        Assert.NotNull(result.RuleEvaluation);
        Assert.Equal("High", result.RuleEvaluation!.PriorityLevel);
        Assert.Equal(75.0, result.RuleEvaluation.TotalScore);
    }

    // ─── Companion can view evaluation ────────────────────────────

    [Fact]
    public async Task Handle_CompanionCanViewEvaluation()
    {
        var sos = BuildSos(1, OwnerId);
        var handler = BuildHandler(
            sosRepo: new StubSosRequestRepository(sos),
            companionRepo: new StubSosRequestCompanionRepository(isCompanion: true));

        var result = await handler.Handle(
            new GetSosEvaluationQuery(1, CompanionId, HasPrivilegedAccess: false),
            CancellationToken.None);

        Assert.Equal(1, result.SosRequestId);
    }

    // ─── Privileged / coordinator can view ────────────────────────

    [Fact]
    public async Task Handle_PrivilegedUserCanViewAnyEvaluation()
    {
        var sos = BuildSos(1, OwnerId);
        var handler = BuildHandler(sosRepo: new StubSosRequestRepository(sos));

        var result = await handler.Handle(
            new GetSosEvaluationQuery(1, CoordinatorId, HasPrivilegedAccess: true),
            CancellationToken.None);

        Assert.Equal(1, result.SosRequestId);
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
                new GetSosEvaluationQuery(1, StrangerId, HasPrivilegedAccess: false),
                CancellationToken.None));
    }

    // ─── SOS not found ────────────────────────────────────────────

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenSosDoesNotExist()
    {
        var handler = BuildHandler(sosRepo: new StubSosRequestRepository(null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new GetSosEvaluationQuery(999, OwnerId, HasPrivilegedAccess: false),
                CancellationToken.None));
    }

    // ─── AI analyses are returned ─────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsAiAnalyses()
    {
        var sos = BuildSos(1, OwnerId);
        var aiAnalyses = new List<SosAiAnalysisModel>
        {
            SosAiAnalysisModel.Create(1, "gemini-2.0", "v1", "priority", "Critical", "Critical", "Urgent case", 0.92),
            SosAiAnalysisModel.Create(1, "gemini-2.0", "v1", "severity", "High", "High", "Severe flooding", 0.85)
        };
        var handler = BuildHandler(
            sosRepo: new StubSosRequestRepository(sos),
            aiRepo: new StubAiAnalysisRepository(aiAnalyses));

        var result = await handler.Handle(
            new GetSosEvaluationQuery(1, OwnerId, HasPrivilegedAccess: false),
            CancellationToken.None);

        Assert.True(result.HasAiAnalysis);
        Assert.Equal(2, result.AiAnalyses.Count);
        Assert.Equal("gemini-2.0", result.AiAnalyses[0].ModelName);
    }

    // ─── No rule evaluation returns null ──────────────────────────

    [Fact]
    public async Task Handle_ReturnsNullRuleEvaluation_WhenDataMissing()
    {
        var sos = BuildSos(1, OwnerId);
        var handler = BuildHandler(
            sosRepo: new StubSosRequestRepository(sos),
            ruleRepo: new StubRuleEvaluationRepository(null));

        var result = await handler.Handle(
            new GetSosEvaluationQuery(1, OwnerId, HasPrivilegedAccess: false),
            CancellationToken.None);

        Assert.Null(result.RuleEvaluation);
        Assert.False(result.HasAiAnalysis);
    }

    // ─── Returns SOS status and priority info ─────────────────────

    [Fact]
    public async Task Handle_ReturnsSosStatusAndPriority()
    {
        var sos = BuildSos(1, OwnerId, SosRequestStatus.InProgress, SosPriorityLevel.Critical);
        sos.SosType = "MEDICAL";
        var handler = BuildHandler(sosRepo: new StubSosRequestRepository(sos));

        var result = await handler.Handle(
            new GetSosEvaluationQuery(1, OwnerId, HasPrivilegedAccess: false),
            CancellationToken.None);

        Assert.Equal("InProgress", result.Status);
        Assert.Equal("Critical", result.CurrentPriorityLevel);
        Assert.Equal("MEDICAL", result.SosType);
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private static SosRequestModel BuildSos(int id, Guid userId,
        SosRequestStatus status = SosRequestStatus.Pending,
        SosPriorityLevel? priority = null)
    {
        var sos = SosRequestModel.Create(userId, HcmLocation, "Cần cứu trợ");
        sos.Id = id;
        sos.Status = status;
        if (priority.HasValue) sos.SetPriorityLevel(priority.Value);
        return sos;
    }

    private static SosRuleEvaluationModel BuildRuleEvaluation(int sosRequestId,
        SosPriorityLevel priority, double totalScore)
    {
        return new SosRuleEvaluationModel
        {
            Id = 1,
            SosRequestId = sosRequestId,
            MedicalScore = 20,
            InjuryScore = 15,
            MobilityScore = 10,
            EnvironmentScore = 20,
            FoodScore = 10,
            TotalScore = totalScore,
            PriorityLevel = priority,
            RuleVersion = "1.0",
            CreatedAt = DateTime.UtcNow
        };
    }

    private static GetSosEvaluationQueryHandler BuildHandler(
        StubSosRequestRepository? sosRepo = null,
        StubSosRequestCompanionRepository? companionRepo = null,
        StubRuleEvaluationRepository? ruleRepo = null,
        StubAiAnalysisRepository? aiRepo = null)
    {
        return new GetSosEvaluationQueryHandler(
            sosRepo ?? new StubSosRequestRepository(null),
            companionRepo ?? new StubSosRequestCompanionRepository(isCompanion: false),
            ruleRepo ?? new StubRuleEvaluationRepository(null),
            aiRepo ?? new StubAiAnalysisRepository([]),
            NullLogger<GetSosEvaluationQueryHandler>.Instance);
    }

    // ─── Stubs ────────────────────────────────────────────────────

    private sealed class StubSosRequestRepository(SosRequestModel? sos) : ISosRequestRepository
    {
        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(sos);
        public Task CreateAsync(SosRequestModel m, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(SosRequestModel m, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid uid, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<PagedResult<SosRequestModel>> GetAllPagedAsync(int pn, int ps, CancellationToken ct = default)
            => Task.FromResult(new PagedResult<SosRequestModel>([], 0, pn, ps));
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int cid, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task UpdateStatusAsync(int id, SosRequestStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusByClusterIdAsync(int cid, SosRequestStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid uid, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<SosRequestModel>());
    }

    private sealed class StubSosRequestCompanionRepository(bool isCompanion) : ISosRequestCompanionRepository
    {
        public Task<bool> IsCompanionAsync(int sosRequestId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(isCompanion);
        public Task CreateRangeAsync(IEnumerable<SosRequestCompanionRecord> c, CancellationToken ct = default) => Task.CompletedTask;
        public Task<List<SosRequestCompanionRecord>> GetBySosRequestIdAsync(int sid, CancellationToken ct = default)
            => Task.FromResult(new List<SosRequestCompanionRecord>());
        public Task<List<int>> GetSosRequestIdsByUserIdAsync(Guid uid, CancellationToken ct = default)
            => Task.FromResult(new List<int>());
    }

    private sealed class StubRuleEvaluationRepository(SosRuleEvaluationModel? eval) : ISosRuleEvaluationRepository
    {
        public Task<SosRuleEvaluationModel?> GetBySosRequestIdAsync(int sosRequestId, CancellationToken ct = default)
            => Task.FromResult(eval);
        public Task CreateAsync(SosRuleEvaluationModel e, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubAiAnalysisRepository(List<SosAiAnalysisModel> analyses) : ISosAiAnalysisRepository
    {
        public Task<IEnumerable<SosAiAnalysisModel>> GetAllBySosRequestIdAsync(int sosRequestId, CancellationToken ct = default)
            => Task.FromResult<IEnumerable<SosAiAnalysisModel>>(analyses);
        public Task<SosAiAnalysisModel?> GetBySosRequestIdAsync(int sosRequestId, CancellationToken ct = default)
            => Task.FromResult(analyses.FirstOrDefault());
        public Task CreateAsync(SosAiAnalysisModel a, CancellationToken ct = default) => Task.CompletedTask;
    }
}
