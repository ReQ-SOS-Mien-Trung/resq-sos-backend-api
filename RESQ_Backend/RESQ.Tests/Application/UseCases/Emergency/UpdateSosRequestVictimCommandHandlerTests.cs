using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Emergency.Commands.UpdateSosRequestVictim;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class UpdateSosRequestVictimCommandHandlerTests
{
    private static readonly GeoLocation HcmLocation = new(10.762622, 106.660172);
    private static readonly Guid OwnerId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid StrangerId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid CompanionId = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    private static SosRequestModel BuildSos(
        int id,
        Guid userId,
        SosRequestStatus status = SosRequestStatus.Pending,
        int? clusterId = null)
    {
        var sos = SosRequestModel.Create(userId, HcmLocation, "Cần cứu trợ");
        sos.Id = id;
        sos.Status = status;
        sos.ClusterId = clusterId;
        return sos;
    }

    private static UpdateSosRequestVictimCommand BuildCommand(int sosId, Guid reporterId, string rawMessage = "Cập nhật tình hình")
        => new UpdateSosRequestVictimCommand(sosId, reporterId, HcmLocation, rawMessage);

    private static UpdateSosRequestVictimCommandHandler BuildHandler(
        ISosRequestRepository sosRepo,
        ISosRequestCompanionRepository companionRepo,
        ISosRequestUpdateRepository? sosUpdateRepo = null,
        ISosRuleEvaluationRepository? evalRepo = null,
        ISosPriorityEvaluationService? evalService = null,
        ISosAiAnalysisQueue? aiQueue = null)
    {
        return new UpdateSosRequestVictimCommandHandler(
            sosRepo,
            companionRepo,
            sosUpdateRepo ?? new StubSosRequestUpdateRepository(),
            evalRepo ?? new StubSosRuleEvaluationRepository(),
            evalService ?? new StubPriorityEvaluationService(),
            aiQueue ?? new StubSosAiAnalysisQueue(),
            new StubSosRequestRealtimeHubService(),
            new StubUnitOfWork());
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenSosRequestDoesNotExist()
    {
        var handler = BuildHandler(
            new StubSosRequestRepository(null),
            new StubSosRequestCompanionRepository(isCompanion: false));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(BuildCommand(999, OwnerId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenRequesterIsNeitherOwnerNorCompanion()
    {
        var sos = BuildSos(1, OwnerId);

        var handler = BuildHandler(
            new StubSosRequestRepository(sos),
            new StubSosRequestCompanionRepository(isCompanion: false));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(BuildCommand(1, StrangerId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenSosHasAlreadyBeenGroupedIntoCluster()
    {
        const int sosId = 1;
        const int clusterId = 10;
        var sos = BuildSos(sosId, OwnerId, clusterId: clusterId);

        var handler = BuildHandler(
            new StubSosRequestRepository(sos),
            new StubSosRequestCompanionRepository(isCompanion: false));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(BuildCommand(sosId, OwnerId), CancellationToken.None));

        Assert.Contains($"#{clusterId}", ex.Message);
    }

    [Theory]
    [InlineData(SosRequestStatus.Assigned)]
    [InlineData(SosRequestStatus.InProgress)]
    [InlineData(SosRequestStatus.Incident)]
    [InlineData(SosRequestStatus.Resolved)]
    [InlineData(SosRequestStatus.Cancelled)]
    public async Task Handle_ThrowsBadRequest_WhenSosStatusIsNotPending(SosRequestStatus status)
    {
        var sos = BuildSos(1, OwnerId, status: status);
        var sosRepo = new StubSosRequestRepository(sos);
        var handler = BuildHandler(
            sosRepo,
            new StubSosRequestCompanionRepository(isCompanion: false));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(BuildCommand(1, OwnerId), CancellationToken.None));

        Assert.False(sosRepo.UpdateAsyncWasCalled);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenOwnerUpdatesPendingUngroupedSos()
    {
        var sos = BuildSos(1, OwnerId);
        var sosRepo = new StubSosRequestRepository(sos);
        var updateRepo = new StubSosRequestUpdateRepository();
        var updatedLocation = new GeoLocation(10.780000, 106.690000);
        var packetId = Guid.Parse("dddddddd-0000-0000-0000-000000000004");
        var clientCreatedAt = new DateTime(2026, 5, 5, 3, 30, 0, DateTimeKind.Utc);

        var handler = BuildHandler(
            sosRepo,
            new StubSosRequestCompanionRepository(isCompanion: false),
            updateRepo);

        var command = new UpdateSosRequestVictimCommand(
            SosRequestId: 1,
            ReporterUserId: OwnerId,
            Location: updatedLocation,
            RawMessage: "  Tình hình nguy cấp, 3 người bị thương  ",
            PacketId: packetId,
            OriginId: "origin-updated",
            LocationAccuracy: 7.5,
            SosType: "Medical",
            StructuredData: """{"severity":"high"}""",
            NetworkMetadata: """{"network":"mesh"}""",
            SenderInfo: """{"user_id":"aaaaaaaa-0000-0000-0000-000000000001"}""",
            Timestamp: 123456789,
            ClientCreatedAt: clientCreatedAt,
            VictimInfo: """{"name":"Victim"}""",
            IsSentOnBehalf: true,
            ReporterInfo: """{"user_id":"aaaaaaaa-0000-0000-0000-000000000001"}""");

        var response = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, response.SosRequestId);
        Assert.Equal("VictimUpdate", response.UpdateType);
        Assert.True(sosRepo.UpdateAsyncWasCalled);
        Assert.NotNull(sosRepo.LastUpdatedSos);
        Assert.Equal(packetId, sosRepo.LastUpdatedSos!.PacketId);
        Assert.Equal(updatedLocation.Latitude, sosRepo.LastUpdatedSos.Location?.Latitude);
        Assert.Equal(updatedLocation.Longitude, sosRepo.LastUpdatedSos.Location?.Longitude);
        Assert.Equal(7.5, sosRepo.LastUpdatedSos.LocationAccuracy);
        Assert.Equal("Medical", sosRepo.LastUpdatedSos.SosType);
        Assert.Equal(command.RawMessage.Trim(), sosRepo.LastUpdatedSos.RawMessage);
        Assert.Equal("""{"severity":"high"}""", sosRepo.LastUpdatedSos.StructuredData);
        Assert.Equal("""{"network":"mesh"}""", sosRepo.LastUpdatedSos.NetworkMetadata);
        Assert.Equal("""{"user_id":"aaaaaaaa-0000-0000-0000-000000000001"}""", sosRepo.LastUpdatedSos.SenderInfo);
        Assert.Equal("""{"name":"Victim"}""", sosRepo.LastUpdatedSos.VictimInfo);
        Assert.Equal("""{"user_id":"aaaaaaaa-0000-0000-0000-000000000001"}""", sosRepo.LastUpdatedSos.ReporterInfo);
        Assert.True(sosRepo.LastUpdatedSos.IsSentOnBehalf);
        Assert.Equal("origin-updated", sosRepo.LastUpdatedSos.OriginId);
        Assert.Equal(123456789, sosRepo.LastUpdatedSos.Timestamp);
        Assert.Equal(clientCreatedAt, sosRepo.LastUpdatedSos.CreatedAt);
        Assert.Equal(response.UpdatedAt, sosRepo.LastUpdatedSos.LastUpdatedAt);

        Assert.NotNull(updateRepo.LastVictimUpdate);
        Assert.Equal(sosRepo.LastUpdatedSos.RawMessage, updateRepo.LastVictimUpdate!.RawMessage);
        Assert.Equal(sosRepo.LastUpdatedSos.StructuredData, updateRepo.LastVictimUpdate.StructuredData);
        Assert.Equal(sosRepo.LastUpdatedSos.Location?.Latitude, updateRepo.LastVictimUpdate.Location?.Latitude);
        Assert.Equal(OwnerId, updateRepo.LastVictimUpdate.UpdatedByUserId);
        Assert.Equal("Owner", updateRepo.LastVictimUpdate.UpdatedByMode);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenCompanionUpdatesPendingUngroupedSos()
    {
        var sos = BuildSos(2, OwnerId);
        var updateRepo = new StubSosRequestUpdateRepository();

        var handler = BuildHandler(
            new StubSosRequestRepository(sos),
            new StubSosRequestCompanionRepository(isCompanion: true),
            updateRepo);

        var response = await handler.Handle(
            BuildCommand(2, CompanionId, "Nước dâng nhanh"),
            CancellationToken.None);

        Assert.Equal(2, response.SosRequestId);
        Assert.Equal("VictimUpdate", response.UpdateType);
        Assert.Equal("Companion", updateRepo.LastVictimUpdate?.UpdatedByMode);
    }

    [Fact]
    public async Task Handle_AppliesPriorityFromEvaluation()
    {
        var sos = BuildSos(1, OwnerId);
        var sosRepo = new StubSosRequestRepository(sos);
        var evalService = new StubPriorityEvaluationService(SosPriorityLevel.Critical, totalScore: 95.0);

        var handler = BuildHandler(
            sosRepo,
            new StubSosRequestCompanionRepository(isCompanion: false),
            evalService: evalService);

        await handler.Handle(BuildCommand(1, OwnerId), CancellationToken.None);

        Assert.Equal(SosPriorityLevel.Critical, sosRepo.LastUpdatedSos?.PriorityLevel);
        Assert.Equal(95.0, sosRepo.LastUpdatedSos?.PriorityScore);
    }

    private sealed class StubSosRequestRepository(SosRequestModel? sos) : ISosRequestRepository
    {
        private readonly SosRequestModel? _sos = sos;
        public bool UpdateAsyncWasCalled { get; private set; }
        public SosRequestModel? LastUpdatedSos { get; private set; }

        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_sos);

        public Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default)
        {
            UpdateAsyncWasCalled = true;
            LastUpdatedSos = sosRequest;
            return Task.CompletedTask;
        }

        public Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int id, SosRequestStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusByClusterIdAsync(int clusterId, SosRequestStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<RESQ.Application.Common.Models.PagedResult<SosRequestModel>> GetAllPagedAsync(int pageNumber, int pageSize, System.Collections.Generic.IReadOnlyCollection<RESQ.Domain.Enum.Emergency.SosRequestStatus>? statuses = null, CancellationToken cancellationToken = default) => Task.FromResult(new RESQ.Application.Common.Models.PagedResult<SosRequestModel>([], 0, pageNumber, pageSize));
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
    }

    private sealed class StubSosRequestCompanionRepository(bool isCompanion) : ISosRequestCompanionRepository
    {
        public Task<bool> IsCompanionAsync(int sosRequestId, Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(isCompanion);

        public Task CreateRangeAsync(IEnumerable<SosRequestCompanionRecord> companions, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<SosRequestCompanionRecord>> GetBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default) => Task.FromResult(new List<SosRequestCompanionRecord>());
        public Task<List<int>> GetSosRequestIdsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<int>());
    }

    private sealed class StubSosRequestUpdateRepository : ISosRequestUpdateRepository
    {
        public SosRequestVictimUpdateModel? LastVictimUpdate { get; private set; }

        public Task AddVictimUpdateAsync(SosRequestVictimUpdateModel update, CancellationToken cancellationToken = default)
        {
            LastVictimUpdate = update;
            return Task.CompletedTask;
        }
        public Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> updates, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(IEnumerable<int> teamIncidentIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(new Dictionary<int, IReadOnlyCollection<int>>());

        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(new Dictionary<int, IReadOnlyCollection<int>>());

        public Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>>(new Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>());
    }

    private sealed class StubSosRuleEvaluationRepository : ISosRuleEvaluationRepository
    {
        public Task CreateAsync(SosRuleEvaluationModel evaluation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<SosRuleEvaluationModel?> GetBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default) => Task.FromResult<SosRuleEvaluationModel?>(null);
    }

    private sealed class StubPriorityEvaluationService(SosPriorityLevel level = SosPriorityLevel.Medium, double totalScore = 50.0) : ISosPriorityEvaluationService
    {
        public Task<SosRuleEvaluationModel> EvaluateAsync(int sosRequestId, string? structuredDataJson, string? sosType, CancellationToken cancellationToken = default)
            => Task.FromResult(new SosRuleEvaluationModel
            {
                SosRequestId = sosRequestId,
                PriorityLevel = level,
                TotalScore = totalScore,
                CreatedAt = DateTime.UtcNow
            });

        public Task<SosRuleEvaluationModel> EvaluateWithConfigAsync(int sosRequestId, string? structuredDataJson, string? sosType, RESQ.Domain.Entities.System.SosPriorityRuleConfigModel? configModel, CancellationToken cancellationToken = default)
            => EvaluateAsync(sosRequestId, structuredDataJson, sosType, cancellationToken);
    }

    private sealed class StubSosAiAnalysisQueue : ISosAiAnalysisQueue
    {
        public ValueTask QueueAsync(SosAiAnalysisTask task) => ValueTask.CompletedTask;
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveAsync() => Task.FromResult(1);
        public IGenericRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();
        public IQueryable<T> Set<T>() where T : class => throw new NotImplementedException();
        public IQueryable<T> SetTracked<T>() where T : class => throw new NotImplementedException();
        public int SaveChangesWithTransaction() => 1;
        public Task<int> SaveChangesWithTransactionAsync() => Task.FromResult(1);
        public void AttachAsUnchanged<TEntity>(TEntity entity) where TEntity : class { }
        public Task ExecuteInTransactionAsync(Func<Task> action) => action();
    }
}
