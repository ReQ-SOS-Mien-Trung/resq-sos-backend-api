using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.UseCases.Emergency.Commands.RemoveSosRequestFromCluster;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class RemoveSosRequestFromClusterCommandHandlerTests
{
    private static readonly Guid CoordinatorId = Guid.Parse("dddddddd-0000-0000-0000-000000000099");

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenClusterDoesNotExist()
    {
        var handler = BuildHandler(
            clusterRepository: new StubSosClusterRepository(null),
            sosRequestRepository: new StubSosRequestRepository());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new RemoveSosRequestFromClusterCommand(7, 101, CoordinatorId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenSosDoesNotExist()
    {
        var handler = BuildHandler(
            clusterRepository: new StubSosClusterRepository(new SosClusterModel { Id = 7, Status = SosClusterStatus.Pending, SosRequestIds = [101] }),
            sosRequestRepository: new StubSosRequestRepository());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new RemoveSosRequestFromClusterCommand(7, 101, CoordinatorId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenSosDoesNotBelongToCluster()
    {
        var cluster = new SosClusterModel { Id = 7, Status = SosClusterStatus.Pending, SosRequestIds = [101] };
        var sos = BuildSosRequest(101, clusterId: 8);
        var handler = BuildHandler(
            clusterRepository: new StubSosClusterRepository(cluster),
            sosRequestRepository: new StubSosRequestRepository(sos));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new RemoveSosRequestFromClusterCommand(7, 101, CoordinatorId), CancellationToken.None));
    }

    [Theory]
    [InlineData(SosClusterStatus.InProgress)]
    [InlineData(SosClusterStatus.Completed)]
    public async Task Handle_ThrowsConflict_WhenClusterStatusIsNotDetachable(SosClusterStatus status)
    {
        var cluster = new SosClusterModel { Id = 7, Status = status, SosRequestIds = [101] };
        var sos = BuildSosRequest(101, clusterId: 7);
        var handler = BuildHandler(
            clusterRepository: new StubSosClusterRepository(cluster),
            sosRequestRepository: new StubSosRequestRepository(sos));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new RemoveSosRequestFromClusterCommand(7, 101, CoordinatorId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RecomputesPendingCluster_WhenClusterStillHasRemainingSos()
    {
        var cluster = new SosClusterModel
        {
            Id = 7,
            Status = SosClusterStatus.Pending,
            CreatedAt = new DateTime(2026, 4, 19, 6, 0, 0, DateTimeKind.Utc),
            SosRequestIds = [101, 102]
        };
        var removed = BuildSosRequest(101, clusterId: 7, lat: 10.0, lon: 106.0, priority: SosPriorityLevel.High);
        var remaining = BuildSosRequest(
            102,
            clusterId: 7,
            lat: 10.2,
            lon: 106.4,
            priority: SosPriorityLevel.Critical,
            structuredData: """{"incident":{"people_count":{"adult":2,"child":1,"elderly":1}}}""");
        var aiHistoryRepository = new StubClusterAiHistoryRepository();
        var clusterRepository = new StubSosClusterRepository(cluster);
        var sosRequestRepository = new StubSosRequestRepository(removed, remaining);
        var unitOfWork = new StubUnitOfWork();
        var handler = BuildHandler(clusterRepository, sosRequestRepository, aiHistoryRepository, unitOfWork);

        var response = await handler.Handle(
            new RemoveSosRequestFromClusterCommand(7, 101, CoordinatorId),
            CancellationToken.None);

        Assert.False(response.IsClusterDeleted);
        Assert.NotNull(response.UpdatedCluster);
        Assert.Equal([102], response.UpdatedCluster!.SosRequestIds);
        Assert.Equal(SosClusterStatus.Pending, response.UpdatedCluster.Status);
        Assert.Equal(10.2, response.UpdatedCluster.CenterLatitude);
        Assert.Equal(106.4, response.UpdatedCluster.CenterLongitude);
        Assert.Equal("Critical", response.UpdatedCluster.SeverityLevel);
        Assert.Equal(4, response.UpdatedCluster.VictimEstimated);
        Assert.Equal(1, response.UpdatedCluster.ChildrenCount);
        Assert.Equal(1, response.UpdatedCluster.ElderlyCount);
        Assert.Null(sosRequestRepository.GetById(101)!.ClusterId);
        Assert.Equal(1, clusterRepository.UpdateCalls);
        Assert.Equal(0, clusterRepository.DeleteCalls);
        Assert.Equal(0, aiHistoryRepository.DeleteCalls);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCalls);
    }

    [Fact]
    public async Task Handle_ResetsSuggestedClusterToPending_AndKeepsAiHistory()
    {
        var cluster = new SosClusterModel
        {
            Id = 7,
            Status = SosClusterStatus.Suggested,
            CreatedAt = DateTime.UtcNow,
            SosRequestIds = [101, 102]
        };
        var removed = BuildSosRequest(101, clusterId: 7);
        var remaining = BuildSosRequest(102, clusterId: 7);
        var aiHistoryRepository = new StubClusterAiHistoryRepository();
        var clusterRepository = new StubSosClusterRepository(cluster);
        var handler = BuildHandler(
            clusterRepository: clusterRepository,
            sosRequestRepository: new StubSosRequestRepository(removed, remaining),
            clusterAiHistoryRepository: aiHistoryRepository,
            unitOfWork: new StubUnitOfWork());

        var response = await handler.Handle(
            new RemoveSosRequestFromClusterCommand(7, 101, CoordinatorId),
            CancellationToken.None);

        Assert.False(response.IsClusterDeleted);
        Assert.Equal(SosClusterStatus.Pending, response.UpdatedCluster!.Status);
        Assert.Equal(0, aiHistoryRepository.DeleteCalls);
    }

    [Fact]
    public async Task Handle_DeletesClusterAndAiHistory_WhenRemovingLastSos()
    {
        var cluster = new SosClusterModel
        {
            Id = 7,
            Status = SosClusterStatus.Suggested,
            CreatedAt = DateTime.UtcNow,
            SosRequestIds = [101]
        };
        var removed = BuildSosRequest(101, clusterId: 7);
        var aiHistoryRepository = new StubClusterAiHistoryRepository();
        var clusterRepository = new StubSosClusterRepository(cluster);
        var handler = BuildHandler(
            clusterRepository: clusterRepository,
            sosRequestRepository: new StubSosRequestRepository(removed),
            clusterAiHistoryRepository: aiHistoryRepository,
            unitOfWork: new StubUnitOfWork());

        var response = await handler.Handle(
            new RemoveSosRequestFromClusterCommand(7, 101, CoordinatorId),
            CancellationToken.None);

        Assert.True(response.IsClusterDeleted);
        Assert.Null(response.UpdatedCluster);
        Assert.Equal(1, aiHistoryRepository.DeleteCalls);
        Assert.Equal(7, aiHistoryRepository.LastDeletedClusterId);
        Assert.Equal(1, clusterRepository.DeleteCalls);
        Assert.Equal(7, clusterRepository.LastDeletedClusterId);
    }

    [Fact]
    public async Task Handle_KeepsIncidentStatus_WhenDetachingIncidentSos()
    {
        var cluster = new SosClusterModel
        {
            Id = 7,
            Status = SosClusterStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            SosRequestIds = [101, 102]
        };
        var removed = BuildSosRequest(101, clusterId: 7, status: SosRequestStatus.Incident);
        var remaining = BuildSosRequest(102, clusterId: 7);
        var sosRequestRepository = new StubSosRequestRepository(removed, remaining);
        var handler = BuildHandler(
            clusterRepository: new StubSosClusterRepository(cluster),
            sosRequestRepository: sosRequestRepository,
            clusterAiHistoryRepository: new StubClusterAiHistoryRepository(),
            unitOfWork: new StubUnitOfWork());

        await handler.Handle(new RemoveSosRequestFromClusterCommand(7, 101, CoordinatorId), CancellationToken.None);

        Assert.Equal(SosRequestStatus.Incident, sosRequestRepository.GetById(101)!.Status);
        Assert.Null(sosRequestRepository.GetById(101)!.ClusterId);
    }

    private static RemoveSosRequestFromClusterCommandHandler BuildHandler(
        StubSosClusterRepository clusterRepository,
        StubSosRequestRepository sosRequestRepository,
        StubClusterAiHistoryRepository? clusterAiHistoryRepository = null,
        StubUnitOfWork? unitOfWork = null)
    {
        return new RemoveSosRequestFromClusterCommandHandler(
            clusterRepository,
            sosRequestRepository,
            clusterAiHistoryRepository ?? new StubClusterAiHistoryRepository(),
            unitOfWork ?? new StubUnitOfWork(),
            NullLogger<RemoveSosRequestFromClusterCommandHandler>.Instance);
    }

    private static SosRequestModel BuildSosRequest(
        int id,
        int? clusterId,
        double lat = 10.0,
        double lon = 106.0,
        SosPriorityLevel priority = SosPriorityLevel.High,
        SosRequestStatus status = SosRequestStatus.Pending,
        string? structuredData = null)
    {
        var sos = SosRequestModel.Create(Guid.NewGuid(), new GeoLocation(lat, lon), $"SOS #{id}");
        sos.Id = id;
        sos.ClusterId = clusterId;
        sos.PriorityLevel = priority;
        sos.Status = status;
        sos.StructuredData = structuredData;
        return sos;
    }

    private sealed class StubSosClusterRepository(SosClusterModel? cluster) : ISosClusterRepository
    {
        private readonly SosClusterModel? _cluster = cluster;

        public int UpdateCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public int? LastDeletedClusterId { get; private set; }

        public Task<SosClusterModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_cluster?.Id == id ? _cluster : null);

        public Task<IEnumerable<SosClusterModel>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<SosClusterModel>());

        public Task<int> CreateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default)
            => Task.FromResult(cluster.Id);

        public Task UpdateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            LastDeletedClusterId = id;
            return Task.CompletedTask;
        }
    }

    private sealed class StubSosRequestRepository(params SosRequestModel[] sosRequests) : ISosRequestRepository
    {
        private readonly Dictionary<int, SosRequestModel> _requests = sosRequests.ToDictionary(request => request.Id);

        public SosRequestModel? GetById(int id) => _requests.GetValueOrDefault(id);

        public Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default)
        {
            _requests[sosRequest.Id] = sosRequest;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<SosRequestModel>());

        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<SosRequestModel>>(_requests.Values);

        public Task<PagedResult<SosRequestModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<SosRequestModel>([], 0, pageNumber, pageSize));

        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(GetById(id));

        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default)
            => Task.FromResult(_requests.Values.Where(request => request.ClusterId == clusterId));

        public Task UpdateStatusAsync(int id, SosRequestStatus status, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateStatusByClusterIdAsync(int clusterId, SosRequestStatus status, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<SosRequestModel>());
    }

    private sealed class StubClusterAiHistoryRepository : IClusterAiHistoryRepository
    {
        public int DeleteCalls { get; private set; }
        public int? LastDeletedClusterId { get; private set; }

        public Task DeleteByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            LastDeletedClusterId = clusterId;
            return Task.CompletedTask;
        }
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public int ExecuteInTransactionCalls { get; private set; }

        public IGenericRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();
        public IQueryable<T> Set<T>() where T : class => throw new NotImplementedException();
        public IQueryable<T> SetTracked<T>() where T : class => throw new NotImplementedException();
        public int SaveChangesWithTransaction() => throw new NotImplementedException();
        public Task<int> SaveChangesWithTransactionAsync() => throw new NotImplementedException();
        public Task<int> SaveAsync() => Task.FromResult(1);
        public void AttachAsUnchanged<TEntity>(TEntity entity) where TEntity : class { }
        public void ClearTrackedChanges() { }

        public async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            ExecuteInTransactionCalls++;
            await action();
        }
    }
}
