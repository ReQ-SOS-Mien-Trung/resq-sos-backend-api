using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Emergency.Commands.GenerateRescueMissionSuggestion;
using RESQ.Application.UseCases.Emergency.Queries.StreamRescueMissionSuggestion;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.System;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class GenerateRescueMissionSuggestionCommandHandlerTests
{
    private static readonly Guid CoordinatorId = Guid.Parse("dddddddd-0000-0000-0000-000000000004");

    // ─── Success sets Cluster.IsMissionCreated = true and saves ───

    [Fact]
    public async Task Handle_Success_SetsClusterIsMissionCreatedAndSaves()
    {
        var cluster = BuildCluster(1);
        var clusterRepo = new StubClusterRepository(cluster);
        var unitOfWork = new StubUnitOfWork();

        var handler = BuildHandler(
            clusterRepo: clusterRepo,
            suggestionService: new StubSuggestionService(BuildSuccessResult()),
            unitOfWork: unitOfWork);

        var result = await handler.Handle(
            new GenerateRescueMissionSuggestionCommand(1, CoordinatorId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.SuggestionId);
        Assert.True(clusterRepo.UpdatedCluster?.IsMissionCreated);
        Assert.True(unitOfWork.SaveCalls >= 1);
    }

    // ─── AI fail does not update cluster ──────────────────────────

    [Fact]
    public async Task Handle_AiFail_DoesNotUpdateCluster()
    {
        var cluster = BuildCluster(1);
        var clusterRepo = new StubClusterRepository(cluster);
        var unitOfWork = new StubUnitOfWork();

        var handler = BuildHandler(
            clusterRepo: clusterRepo,
            suggestionService: new StubSuggestionService(BuildFailResult()),
            unitOfWork: unitOfWork);

        var result = await handler.Handle(
            new GenerateRescueMissionSuggestionCommand(1, CoordinatorId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(clusterRepo.UpdatedCluster);
        Assert.Equal(0, unitOfWork.SaveCalls);
    }

    // ─── Maps response fields correctly ───────────────────────────

    [Fact]
    public async Task Handle_MapsResponseFields()
    {
        var successResult = BuildSuccessResult();
        successResult.SuggestedMissionTitle = "Cứu hộ khu vực ngập";
        successResult.SuggestedMissionType = "RESCUE";
        successResult.ConfidenceScore = 0.85;

        var handler = BuildHandler(
            suggestionService: new StubSuggestionService(successResult));

        var result = await handler.Handle(
            new GenerateRescueMissionSuggestionCommand(1, CoordinatorId),
            CancellationToken.None);

        Assert.Equal("Cứu hộ khu vực ngập", result.SuggestedMissionTitle);
        Assert.Equal("RESCUE", result.SuggestedMissionType);
        Assert.Equal(0.85, result.ConfidenceScore);
    }

    // ═══ Stream handler tests ═════════════════════════════════════

    [Fact]
    public async Task Stream_EmitsLoadingContext_Then_Done()
    {
        var cluster = BuildCluster(1);
        var aiResult = BuildSuccessResult();
        var clusterRepo = new StubClusterRepository(cluster);

        var streamHandler = BuildStreamHandler(
            clusterRepo: clusterRepo,
            suggestionService: new StubStreamingSuggestionService([
                new SseMissionEvent { EventType = "chunk", Data = "generating..." },
                new SseMissionEvent { EventType = "result", Result = aiResult }
            ]));

        var events = new List<SseMissionEvent>();
        await foreach (var evt in streamHandler.Handle(
            new StreamRescueMissionSuggestionQuery(1), CancellationToken.None))
        {
            events.Add(evt);
        }

        Assert.Equal("status", events[0].EventType);
        Assert.Equal("loading_context", events[0].Data);
        Assert.Equal("status", events.Last().EventType);
        Assert.Equal("done", events.Last().Data);
        Assert.True(clusterRepo.UpdatedCluster?.IsMissionCreated);
    }

    // ─── Stream: error event stops stream ─────────────────────────

    [Fact]
    public async Task Stream_Error_StopsAndDoesNotUpdateCluster()
    {
        var cluster = BuildCluster(1);
        var clusterRepo = new StubClusterRepository(cluster);

        var streamHandler = BuildStreamHandler(
            clusterRepo: clusterRepo,
            suggestionService: new StubStreamingSuggestionService([
                new SseMissionEvent { EventType = "error", Data = "AI service unavailable" }
            ]));

        var events = new List<SseMissionEvent>();
        await foreach (var evt in streamHandler.Handle(
            new StreamRescueMissionSuggestionQuery(1), CancellationToken.None))
        {
            events.Add(evt);
        }

        Assert.Contains(events, e => e.EventType == "error");
        Assert.DoesNotContain(events, e => e.EventType == "status" && e.Data == "done");
        Assert.Null(clusterRepo.UpdatedCluster);
    }

    // ─── Stream: context error emits error and stops ──────────────

    [Fact]
    public async Task Stream_ContextError_EmitsErrorAndStops()
    {
        var streamHandler = BuildStreamHandler(
            contextService: new StubContextService(throwOnPrepare: true));

        var events = new List<SseMissionEvent>();
        await foreach (var evt in streamHandler.Handle(
            new StreamRescueMissionSuggestionQuery(99), CancellationToken.None))
        {
            events.Add(evt);
        }

        Assert.Equal("status", events[0].EventType);
        Assert.Equal("loading_context", events[0].Data);
        Assert.Equal("error", events[1].EventType);
        Assert.Equal(2, events.Count);
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private static SosClusterModel BuildCluster(int id) => new()
    {
        Id = id,
        CenterLatitude = 10.76,
        CenterLongitude = 106.66,
        RadiusKm = 5,
        SeverityLevel = "High",
        IsMissionCreated = false,
        SosRequestIds = [1, 2]
    };

    private static RescueMissionSuggestionResult BuildSuccessResult() => new()
    {
        IsSuccess = true,
        SuggestionId = 42,
        ModelName = "gemini-2.0",
        ResponseTimeMs = 1500
    };

    private static RescueMissionSuggestionResult BuildFailResult() => new()
    {
        IsSuccess = false,
        ErrorMessage = "AI generation failed",
        ModelName = "gemini-2.0",
        ResponseTimeMs = 500
    };

    private static MissionContext BuildContext(SosClusterModel? cluster = null) => new()
    {
        Cluster = cluster ?? BuildCluster(1),
        SosRequests = [new SosRequestSummary { Id = 1, Latitude = 10.76, Longitude = 106.66 }],
        NearbyDepots = [],
        NearbyTeams = [],
        MultiDepotRecommended = false
    };

    private static GenerateRescueMissionSuggestionCommandHandler BuildHandler(
        StubClusterRepository? clusterRepo = null,
        StubSuggestionService? suggestionService = null,
        StubUnitOfWork? unitOfWork = null)
    {
        var cluster = BuildCluster(1);
        return new GenerateRescueMissionSuggestionCommandHandler(
            clusterRepo ?? new StubClusterRepository(cluster),
            new StubContextService(),
            suggestionService ?? new StubSuggestionService(BuildSuccessResult()),
            unitOfWork ?? new StubUnitOfWork(),
            NullLogger<GenerateRescueMissionSuggestionCommandHandler>.Instance);
    }

    private static StreamRescueMissionSuggestionQueryHandler BuildStreamHandler(
        StubClusterRepository? clusterRepo = null,
        StubStreamingSuggestionService? suggestionService = null,
        StubContextService? contextService = null,
        StubUnitOfWork? unitOfWork = null)
    {
        var cluster = BuildCluster(1);
        return new StreamRescueMissionSuggestionQueryHandler(
            contextService ?? new StubContextService(),
            suggestionService ?? new StubStreamingSuggestionService([]),
            clusterRepo ?? new StubClusterRepository(cluster),
            unitOfWork ?? new StubUnitOfWork(),
            NullLogger<StreamRescueMissionSuggestionQueryHandler>.Instance);
    }

    // ─── Stubs ────────────────────────────────────────────────────

    private sealed class StubClusterRepository(SosClusterModel cluster) : ISosClusterRepository
    {
        public SosClusterModel? UpdatedCluster { get; private set; }

        public Task<IEnumerable<SosClusterModel>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IEnumerable<SosClusterModel>>([cluster]);
        public Task<SosClusterModel?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult<SosClusterModel?>(cluster);
        public Task<int> CreateAsync(SosClusterModel c, CancellationToken ct = default) => Task.FromResult(c.Id);
        public Task UpdateAsync(SosClusterModel c, CancellationToken ct = default)
        {
            UpdatedCluster = c;
            return Task.CompletedTask;
        }
    }

    private sealed class StubContextService(bool throwOnPrepare = false) : IMissionContextService
    {
        public Task<MissionContext> PrepareContextAsync(int clusterId, CancellationToken ct = default)
        {
            if (throwOnPrepare) throw new Exception("Cluster not found");
            return Task.FromResult(BuildContext());
        }
    }

    private sealed class StubSuggestionService(RescueMissionSuggestionResult result) : IRescueMissionSuggestionService
    {
        public Task<RescueMissionSuggestionResult> GenerateSuggestionAsync(
            List<SosRequestSummary> sos, List<DepotSummary>? depots,
            List<AgentTeamInfo>? teams, bool isMultiDepot, int? clusterId,
            CancellationToken ct = default)
            => Task.FromResult(result);

        public Task<RescueMissionSuggestionResult> PreviewSuggestionAsync(
            List<SosRequestSummary> sos, List<DepotSummary>? depots,
            List<AgentTeamInfo>? teams, bool isMultiDepot, int clusterId,
            PromptModel promptOverride, AiConfigModel? aiConfigOverride = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<SseMissionEvent> GenerateSuggestionStreamAsync(
            List<SosRequestSummary> sos, List<DepotSummary>? depots,
            List<AgentTeamInfo>? teams, bool isMultiDepot, int? clusterId,
            CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class StubStreamingSuggestionService(List<SseMissionEvent> events) : IRescueMissionSuggestionService
    {
        public Task<RescueMissionSuggestionResult> GenerateSuggestionAsync(
            List<SosRequestSummary> sos, List<DepotSummary>? depots,
            List<AgentTeamInfo>? teams, bool isMultiDepot, int? clusterId,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<RescueMissionSuggestionResult> PreviewSuggestionAsync(
            List<SosRequestSummary> sos, List<DepotSummary>? depots,
            List<AgentTeamInfo>? teams, bool isMultiDepot, int clusterId,
            PromptModel promptOverride, AiConfigModel? aiConfigOverride = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public async IAsyncEnumerable<SseMissionEvent> GenerateSuggestionStreamAsync(
            List<SosRequestSummary> sos, List<DepotSummary>? depots,
            List<AgentTeamInfo>? teams, bool isMultiDepot, int? clusterId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var evt in events)
            {
                yield return evt;
            }
            await Task.CompletedTask;
        }
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public Task<int> SaveAsync() { SaveCalls++; return Task.FromResult(1); }
        public IGenericRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();
        public IQueryable<T> Set<T>() where T : class => throw new NotImplementedException();
        public IQueryable<T> SetTracked<T>() where T : class => throw new NotImplementedException();
        public int SaveChangesWithTransaction() => 1;
        public Task<int> SaveChangesWithTransactionAsync() => Task.FromResult(1);
        public void AttachAsUnchanged<TEntity>(TEntity entity) where TEntity : class { }
        public Task ExecuteInTransactionAsync(Func<Task> action) => action();
    }
}
