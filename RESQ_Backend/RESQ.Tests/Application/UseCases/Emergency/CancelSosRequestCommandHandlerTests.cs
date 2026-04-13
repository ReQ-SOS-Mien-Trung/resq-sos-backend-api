using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Emergency.Commands.CancelSosRequest;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class CancelSosRequestCommandHandlerTests
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

    private static CancelSosRequestCommandHandler BuildHandler(
        ISosRequestRepository sosRepo,
        ISosRequestCompanionRepository companionRepo)
    {
        var logger = NullLogger<CancelSosRequestCommandHandler>.Instance;
        var sosUpdateRepo = new StubSosRequestUpdateRepository();
        var missionActivityRepo = new StubMissionActivityRepository();
        var teamIncidentRepo = new StubTeamIncidentRepository();
        var unitOfWork = new StubUnitOfWork();

        return new CancelSosRequestCommandHandler(
            sosRepo,
            companionRepo,
            sosUpdateRepo,
            missionActivityRepo,
            teamIncidentRepo,
            unitOfWork,
            logger);
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenSosRequestDoesNotExist()
    {
        var handler = BuildHandler(
            new StubSosRequestRepository(null),
            new StubSosRequestCompanionRepository(isCompanion: false));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CancelSosRequestCommand(999, OwnerId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenRequesterIsNeitherOwnerNorCompanion()
    {
        var sos = BuildSos(1, OwnerId);

        var handler = BuildHandler(
            new StubSosRequestRepository(sos),
            new StubSosRequestCompanionRepository(isCompanion: false));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new CancelSosRequestCommand(1, StrangerId), CancellationToken.None));
    }

    [Theory]
    [InlineData(SosRequestStatus.Assigned)]
    [InlineData(SosRequestStatus.InProgress)]
    [InlineData(SosRequestStatus.Resolved)]
    [InlineData(SosRequestStatus.Cancelled)]
    [InlineData(SosRequestStatus.Incident)]
    public async Task Handle_ThrowsBadRequest_WhenSosStatusIsNotCancellable(SosRequestStatus status)
    {
        var sos = BuildSos(1, OwnerId, status);
        var sosRepo = new StubSosRequestRepository(sos);

        var handler = BuildHandler(
            sosRepo,
            new StubSosRequestCompanionRepository(isCompanion: false));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new CancelSosRequestCommand(1, OwnerId), CancellationToken.None));

        Assert.False(sosRepo.UpdateStatusWasCalled);
    }

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenSosHasAlreadyBeenGroupedIntoCluster()
    {
        const int clusterId = 7;
        var sos = BuildSos(1, OwnerId, clusterId: clusterId);
        var sosRepo = new StubSosRequestRepository(sos);

        var handler = BuildHandler(
            sosRepo,
            new StubSosRequestCompanionRepository(isCompanion: false));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new CancelSosRequestCommand(1, OwnerId), CancellationToken.None));

        Assert.Contains($"#{clusterId}", ex.Message);
        Assert.False(sosRepo.UpdateStatusWasCalled);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenOwnerCancelsPendingUngroupedSos()
    {
        var sos = BuildSos(1, OwnerId);
        var sosRepo = new StubSosRequestRepository(sos);

        var handler = BuildHandler(
            sosRepo,
            new StubSosRequestCompanionRepository(isCompanion: false));

        var response = await handler.Handle(new CancelSosRequestCommand(1, OwnerId), CancellationToken.None);

        Assert.Equal(1, response.SosRequestId);
        Assert.Equal("Cancelled", response.Status);
        Assert.True(sosRepo.UpdateStatusWasCalled);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenCompanionCancelsPendingUngroupedSos()
    {
        var sos = BuildSos(2, OwnerId);
        var sosRepo = new StubSosRequestRepository(sos);

        var handler = BuildHandler(
            sosRepo,
            new StubSosRequestCompanionRepository(isCompanion: true));

        var response = await handler.Handle(new CancelSosRequestCommand(2, CompanionId), CancellationToken.None);

        Assert.Equal(2, response.SosRequestId);
        Assert.Equal("Cancelled", response.Status);
        Assert.True(sosRepo.UpdateStatusWasCalled);
    }

    private sealed class StubSosRequestRepository(SosRequestModel? sos) : ISosRequestRepository
    {
        private readonly SosRequestModel? _sos = sos;
        public bool UpdateStatusWasCalled { get; private set; }

        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_sos);

        public Task UpdateStatusAsync(int id, SosRequestStatus status, CancellationToken cancellationToken = default)
        {
            UpdateStatusWasCalled = true;
            return Task.CompletedTask;
        }

        public Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<RESQ.Application.Common.Models.PagedResult<SosRequestModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult(new RESQ.Application.Common.Models.PagedResult<SosRequestModel>([], 0, pageNumber, pageSize));
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task UpdateStatusByClusterIdAsync(int clusterId, SosRequestStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
        public Task AddVictimUpdateAsync(SosRequestVictimUpdateModel update, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> updates, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(IEnumerable<int> teamIncidentIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(new Dictionary<int, IReadOnlyCollection<int>>());

        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(new Dictionary<int, IReadOnlyCollection<int>>());

        public Task<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>> GetLatestVictimUpdatesBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>>(new Dictionary<int, SosRequestVictimUpdateModel>());

        public Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>>(new Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>());
    }

    private sealed class StubMissionActivityRepository : IMissionActivityRepository
    {
        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<MissionActivityModel?>(null);
        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionActivityModel>());
        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionActivityModel>());
        public Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MissionActivityModel>>([]);
        public Task<int> AddAsync(MissionActivityModel activity, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task UpdateAsync(MissionActivityModel activity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int activityId, MissionActivityStatus status, Guid decisionBy, string? imageUrl = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AssignTeamAsync(int activityId, int missionTeamId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> activityIds, Guid decisionBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubTeamIncidentRepository : ITeamIncidentRepository
    {
        public Task<IEnumerable<TeamIncidentModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<TeamIncidentModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<TeamIncidentModel?>(null);
        public Task<IEnumerable<TeamIncidentModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<IEnumerable<TeamIncidentModel>> GetByMissionTeamIdAsync(int missionTeamId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<int> CreateAsync(TeamIncidentModel model, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task UpdateStatusAsync(int id, TeamIncidentStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateSupportSosRequestIdAsync(int id, int? supportSosRequestId, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
