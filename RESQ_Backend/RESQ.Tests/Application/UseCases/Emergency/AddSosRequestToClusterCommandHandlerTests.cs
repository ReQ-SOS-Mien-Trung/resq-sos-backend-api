using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.Emergency.Commands.AddSosRequestToCluster;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class AddSosRequestToClusterCommandHandlerTests
{
    private static readonly Guid CoordinatorId = Guid.Parse("dddddddd-0000-0000-0000-000000000099");

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenClusterDoesNotExist()
    {
        var handler = BuildHandler(
            clusterRepository: new StubSosClusterRepository(null),
            sosRequestRepository: new StubSosRequestRepository());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new AddSosRequestToClusterCommand(7, 101, CoordinatorId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenSosDoesNotExist()
    {
        var handler = BuildHandler(
            clusterRepository: new StubSosClusterRepository(new SosClusterModel { Id = 7, Status = SosClusterStatus.Pending, SosRequestIds = [201] }),
            sosRequestRepository: new StubSosRequestRepository());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new AddSosRequestToClusterCommand(7, 101, CoordinatorId), CancellationToken.None));
    }

    [Theory]
    [InlineData(SosClusterStatus.InProgress)]
    [InlineData(SosClusterStatus.Completed)]
    public async Task Handle_ThrowsConflict_WhenClusterStatusIsNotAttachable(SosClusterStatus status)
    {
        var cluster = new SosClusterModel { Id = 7, Status = status, SosRequestIds = [201] };
        var existing = BuildSosRequest(201, clusterId: 7);
        var incoming = BuildSosRequest(101, clusterId: null);
        var handler = BuildHandler(
            clusterRepository: new StubSosClusterRepository(cluster),
            sosRequestRepository: new StubSosRequestRepository(existing, incoming));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new AddSosRequestToClusterCommand(7, 101, CoordinatorId), CancellationToken.None));
    }

    [Theory]
    [InlineData(SosRequestStatus.Assigned)]
    [InlineData(SosRequestStatus.InProgress)]
    [InlineData(SosRequestStatus.Resolved)]
    [InlineData(SosRequestStatus.Cancelled)]
    public async Task Handle_ThrowsBadRequest_WhenSosStatusIsNotPendingOrIncident(SosRequestStatus status)
    {
        var cluster = new SosClusterModel { Id = 7, Status = SosClusterStatus.Pending, SosRequestIds = [201] };
        var existing = BuildSosRequest(201, clusterId: 7);
        var incoming = BuildSosRequest(101, clusterId: null, status: status);
        var handler = BuildHandler(
            clusterRepository: new StubSosClusterRepository(cluster),
            sosRequestRepository: new StubSosRequestRepository(existing, incoming));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new AddSosRequestToClusterCommand(7, 101, CoordinatorId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsConflict_WhenSosAlreadyBelongsToSameCluster()
    {
        var cluster = new SosClusterModel { Id = 7, Status = SosClusterStatus.Pending, SosRequestIds = [101, 201] };
        var existing = BuildSosRequest(201, clusterId: 7);
        var incoming = BuildSosRequest(101, clusterId: 7);
        var handler = BuildHandler(
            clusterRepository: new StubSosClusterRepository(cluster),
            sosRequestRepository: new StubSosRequestRepository(existing, incoming));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new AddSosRequestToClusterCommand(7, 101, CoordinatorId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsConflict_WhenSosAlreadyBelongsToAnotherCluster()
    {
        var cluster = new SosClusterModel { Id = 7, Status = SosClusterStatus.Pending, SosRequestIds = [201] };
        var existing = BuildSosRequest(201, clusterId: 7);
        var incoming = BuildSosRequest(101, clusterId: 8);
        var handler = BuildHandler(
            clusterRepository: new StubSosClusterRepository(cluster),
            sosRequestRepository: new StubSosRequestRepository(existing, incoming));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new AddSosRequestToClusterCommand(7, 101, CoordinatorId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenCombinedRequestsExceedConfiguredSpreadDistance()
    {
        var cluster = new SosClusterModel { Id = 7, Status = SosClusterStatus.Pending, SosRequestIds = [201] };
        var existing = BuildSosRequest(201, clusterId: 7, lat: 10.0, lon: 106.0);
        var incoming = BuildSosRequest(101, clusterId: null, lat: 10.3, lon: 106.3);
        var handler = BuildHandler(
            clusterRepository: new StubSosClusterRepository(cluster),
            sosRequestRepository: new StubSosRequestRepository(existing, incoming),
            groupingConfigRepository: new StubSosClusterGroupingConfigRepository(maximumDistanceKm: 5));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new AddSosRequestToClusterCommand(7, 101, CoordinatorId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AddsPendingSosToPendingCluster_AndRecomputesAggregate()
    {
        var cluster = new SosClusterModel
        {
            Id = 7,
            Status = SosClusterStatus.Pending,
            CreatedAt = new DateTime(2026, 4, 19, 6, 0, 0, DateTimeKind.Utc),
            SosRequestIds = [201]
        };
        var existing = BuildSosRequest(
            201,
            clusterId: 7,
            lat: 10.0,
            lon: 106.0,
            priority: SosPriorityLevel.High,
            structuredData: """{"incident":{"people_count":{"adult":1,"child":0,"elderly":0}}}""");
        var incoming = BuildSosRequest(
            101,
            clusterId: null,
            lat: 10.02,
            lon: 106.04,
            priority: SosPriorityLevel.Critical,
            status: SosRequestStatus.Pending,
            structuredData: """{"incident":{"people_count":{"adult":2,"child":1,"elderly":1}}}""");
        var clusterRepository = new StubSosClusterRepository(cluster);
        var sosRequestRepository = new StubSosRequestRepository(existing, incoming);
        var unitOfWork = new StubUnitOfWork();
        var handler = BuildHandler(
            clusterRepository: clusterRepository,
            sosRequestRepository: sosRequestRepository,
            unitOfWork: unitOfWork);

        var response = await handler.Handle(
            new AddSosRequestToClusterCommand(7, 101, CoordinatorId),
            CancellationToken.None);

        Assert.NotNull(response.UpdatedCluster);
        Assert.Equal(7, response.ClusterId);
        Assert.Equal(101, response.AddedSosRequestId);
        Assert.Equal(SosClusterStatus.Pending, response.UpdatedCluster!.Status);
        Assert.Equal(2, response.UpdatedCluster.SosRequestCount);
        Assert.Equal([201, 101], response.UpdatedCluster.SosRequestIds);
        Assert.Equal(10.01, response.UpdatedCluster.CenterLatitude!.Value, 3);
        Assert.Equal(106.02, response.UpdatedCluster.CenterLongitude!.Value, 3);
        Assert.Equal("Critical", response.UpdatedCluster.SeverityLevel);
        Assert.Equal(5, response.UpdatedCluster.VictimEstimated);
        Assert.Equal(1, response.UpdatedCluster.ChildrenCount);
        Assert.Equal(1, response.UpdatedCluster.ElderlyCount);
        Assert.Equal(7, sosRequestRepository.GetById(101)!.ClusterId);
        Assert.Equal(SosRequestStatus.Pending, sosRequestRepository.GetById(101)!.Status);
        Assert.Equal(1, clusterRepository.UpdateCalls);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCalls);
    }

    [Fact]
    public async Task Handle_ResetsSuggestedClusterToPending_WhenAddingIncidentSos()
    {
        var cluster = new SosClusterModel
        {
            Id = 7,
            Status = SosClusterStatus.Suggested,
            CreatedAt = DateTime.UtcNow,
            SosRequestIds = [201]
        };
        var existing = BuildSosRequest(201, clusterId: 7);
        var incoming = BuildSosRequest(101, clusterId: null, status: SosRequestStatus.Incident);
        var handler = BuildHandler(
            clusterRepository: new StubSosClusterRepository(cluster),
            sosRequestRepository: new StubSosRequestRepository(existing, incoming),
            unitOfWork: new StubUnitOfWork());

        var response = await handler.Handle(
            new AddSosRequestToClusterCommand(7, 101, CoordinatorId),
            CancellationToken.None);

        Assert.Equal(SosClusterStatus.Pending, response.UpdatedCluster!.Status);
        Assert.Equal(SosRequestStatus.Incident, incoming.Status);
        Assert.Equal(7, incoming.ClusterId);
    }

    private static AddSosRequestToClusterCommandHandler BuildHandler(
        StubSosClusterRepository clusterRepository,
        StubSosRequestRepository sosRequestRepository,
        StubSosClusterGroupingConfigRepository? groupingConfigRepository = null,
        StubUnitOfWork? unitOfWork = null)
    {
        return new AddSosRequestToClusterCommandHandler(
            clusterRepository,
            sosRequestRepository,
            groupingConfigRepository ?? new StubSosClusterGroupingConfigRepository(),
            unitOfWork ?? new StubUnitOfWork(),
            NullLogger<AddSosRequestToClusterCommandHandler>.Instance);
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
            => Task.CompletedTask;
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

    private sealed class StubSosClusterGroupingConfigRepository(double maximumDistanceKm = 10.0)
        : ISosClusterGroupingConfigRepository
    {
        public Task<SosClusterGroupingConfigDto?> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SosClusterGroupingConfigDto?>(new SosClusterGroupingConfigDto
            {
                MaximumDistanceKm = maximumDistanceKm,
                UpdatedAt = DateTime.UtcNow
            });
        }

        public Task<SosClusterGroupingConfigDto> UpsertAsync(
            double maximumDistanceKm,
            Guid updatedBy,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
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
