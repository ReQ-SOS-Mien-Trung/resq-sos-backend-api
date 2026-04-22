using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.Emergency.Commands.CreateSosCluster;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Emergency;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class CreateSosClusterCommandHandlerTests
{
    private static readonly Guid CoordinatorId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

    private static SosRequestModel BuildPendingSos(int id, double lat, double lon, int? clusterId = null)
    {
        var sos = SosRequestModel.Create(Guid.NewGuid(), new GeoLocation(lat, lon), "Cần cứu trợ");
        sos.Id = id;
        sos.Status = SosRequestStatus.Pending;
        sos.ClusterId = clusterId;
        return sos;
    }

    private static CreateSosClusterCommandHandler BuildHandler(
        ISosRequestRepository sosRepo,
        ISosClusterRepository clusterRepo,
        ISosClusterGroupingConfigRepository? configRepo = null)
    {
        return new CreateSosClusterCommandHandler(
            clusterRepo ?? new StubSosClusterRepository(),
            sosRepo,
            configRepo ?? new StubSosClusterGroupingConfigRepository(null),
            new StubAdminRealtimeHubService(),
            new StubUnitOfWork(),
            NullLogger<CreateSosClusterCommandHandler>.Instance);
    }

    // -- Not Found --------------------------------------------------------------

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenAnySosRequestDoesNotExist()
    {
        // ID 99 is unknown - repository will return null for it
        var repo = new StubSosRequestRepository(new Dictionary<int, SosRequestModel>
        {
            [1] = BuildPendingSos(1, 10.762622, 106.660172)
        });

        var handler = BuildHandler(repo, new StubSosClusterRepository());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CreateSosClusterCommand([1, 99], CoordinatorId), CancellationToken.None));
    }

    // -- Status validation ------------------------------------------------------

    [Theory]
    [InlineData(SosRequestStatus.Assigned)]
    [InlineData(SosRequestStatus.InProgress)]
    [InlineData(SosRequestStatus.Resolved)]
    [InlineData(SosRequestStatus.Cancelled)]
    public async Task Handle_ThrowsBadRequest_WhenSosIsNotPendingOrIncident(SosRequestStatus status)
    {
        var sos = BuildPendingSos(1, 10.762622, 106.660172);
        sos.Status = status;

        var repo = new StubSosRequestRepository(new Dictionary<int, SosRequestModel> { [1] = sos });
        var handler = BuildHandler(repo, new StubSosClusterRepository());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new CreateSosClusterCommand([1], CoordinatorId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Succeeds_WhenSosStatusIsIncident()
    {
        var sos = BuildPendingSos(1, 10.762622, 106.660172);
        sos.Status = SosRequestStatus.Incident;

        var repo = new StubSosRequestRepository(new Dictionary<int, SosRequestModel> { [1] = sos });
        var clusterRepo = new StubSosClusterRepository(returnedClusterId: 7);
        var handler = BuildHandler(repo, clusterRepo);

        var response = await handler.Handle(new CreateSosClusterCommand([1], CoordinatorId), CancellationToken.None);

        Assert.Equal(7, response.ClusterId);
    }

    // -- Already clustered ------------------------------------------------------

    [Fact]
    public async Task Handle_ThrowsConflict_WhenSosAlreadyBelongsToAnotherCluster()
    {
        var sos = BuildPendingSos(1, 10.762622, 106.660172, clusterId: 5);

        var repo = new StubSosRequestRepository(new Dictionary<int, SosRequestModel> { [1] = sos });
        var handler = BuildHandler(repo, new StubSosClusterRepository());

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new CreateSosClusterCommand([1], CoordinatorId), CancellationToken.None));
    }

    // -- Distance validation ----------------------------------------------------

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenTwoSosRequestsAreTooFarApart()
    {
        // ~16 km apart (0.15 degrees latitude ≈ 16.7 km) - exceeds default 10 km
        var sos1 = BuildPendingSos(1, 10.0, 106.0);
        var sos2 = BuildPendingSos(2, 10.15, 106.0);

        var repo = new StubSosRequestRepository(new Dictionary<int, SosRequestModel>
        {
            [1] = sos1,
            [2] = sos2
        });
        var handler = BuildHandler(repo, new StubSosClusterRepository());

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new CreateSosClusterCommand([1, 2], CoordinatorId), CancellationToken.None));

        Assert.Contains("vượt quá giới hạn", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_Succeeds_WhenSosRequestsAreWithinConfiguredDistance()
    {
        // ~1.1 km apart - well within 10 km limit
        var sos1 = BuildPendingSos(1, 10.0, 106.0);
        var sos2 = BuildPendingSos(2, 10.01, 106.0);

        var repo = new StubSosRequestRepository(new Dictionary<int, SosRequestModel>
        {
            [1] = sos1,
            [2] = sos2
        });
        var clusterRepo = new StubSosClusterRepository(returnedClusterId: 3);
        var handler = BuildHandler(repo, clusterRepo);

        var response = await handler.Handle(new CreateSosClusterCommand([1, 2], CoordinatorId), CancellationToken.None);

        Assert.Equal(3, response.ClusterId);
        Assert.Equal(2, response.SosRequestCount);
        Assert.Contains(1, response.SosRequestIds);
        Assert.Contains(2, response.SosRequestIds);
    }

    // -- Custom config distance --------------------------------------------------

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenDistanceExceedsCustomConfiguredLimit()
    {
        // ~1.1 km apart, but config sets max 0.5 km
        var sos1 = BuildPendingSos(1, 10.0, 106.0);
        var sos2 = BuildPendingSos(2, 10.01, 106.0);

        var repo = new StubSosRequestRepository(new Dictionary<int, SosRequestModel>
        {
            [1] = sos1,
            [2] = sos2
        });
        var configRepo = new StubSosClusterGroupingConfigRepository(maximumDistanceKm: 0.5);
        var handler = BuildHandler(repo, new StubSosClusterRepository(), configRepo);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new CreateSosClusterCommand([1, 2], CoordinatorId), CancellationToken.None));
    }

    // -- Response shape ---------------------------------------------------------

    [Fact]
    public async Task Handle_ReturnsCenterCoordinates_BasedOnAverageOfAllSosLocations()
    {
        var sos1 = BuildPendingSos(1, 10.0, 106.0);
        var sos2 = BuildPendingSos(2, 10.002, 106.002);

        var repo = new StubSosRequestRepository(new Dictionary<int, SosRequestModel>
        {
            [1] = sos1,
            [2] = sos2
        });
        var clusterRepo = new StubSosClusterRepository(returnedClusterId: 1);
        var handler = BuildHandler(repo, clusterRepo);

        await handler.Handle(new CreateSosClusterCommand([1, 2], CoordinatorId), CancellationToken.None);

        // Center should be the average of the two SOS request coordinates
        Assert.NotNull(clusterRepo.LastCreatedCluster);
        Assert.Equal(10.001, clusterRepo.LastCreatedCluster!.CenterLatitude!.Value, precision: 5);
        Assert.Equal(106.001, clusterRepo.LastCreatedCluster!.CenterLongitude!.Value, precision: 5);
        Assert.Equal(SosClusterStatus.Pending, clusterRepo.LastCreatedCluster!.Status);
    }

    // -- Stubs ------------------------------------------------------------------

    private sealed class StubSosRequestRepository(Dictionary<int, SosRequestModel>? store = null) : ISosRequestRepository
    {
        private readonly Dictionary<int, SosRequestModel> _store = store ?? [];

        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.TryGetValue(id, out var sos) ? sos : null);

        public Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int id, SosRequestStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusByClusterIdAsync(int clusterId, SosRequestStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<RESQ.Application.Common.Models.PagedResult<SosRequestModel>> GetAllPagedAsync(int pageNumber, int pageSize, System.Collections.Generic.IReadOnlyCollection<RESQ.Domain.Enum.Emergency.SosRequestStatus>? statuses = null, CancellationToken cancellationToken = default) => Task.FromResult(new RESQ.Application.Common.Models.PagedResult<SosRequestModel>([], 0, pageNumber, pageSize));
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
    }

    private sealed class StubSosClusterRepository(int returnedClusterId = 1) : ISosClusterRepository
    {
        public SosClusterModel? LastCreatedCluster { get; private set; }

        public Task<int> CreateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default)
        {
            LastCreatedCluster = cluster;
            cluster.Id = returnedClusterId;
            return Task.FromResult(returnedClusterId);
        }

        public Task<SosClusterModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<SosClusterModel?>(null);
        public Task<IEnumerable<SosClusterModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosClusterModel>());
        public Task UpdateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubSosClusterGroupingConfigRepository(double? maximumDistanceKm) : ISosClusterGroupingConfigRepository
    {
        public Task<SosClusterGroupingConfigDto?> GetAsync(CancellationToken cancellationToken = default)
        {
            if (!maximumDistanceKm.HasValue)
                return Task.FromResult<SosClusterGroupingConfigDto?>(null);

            return Task.FromResult<SosClusterGroupingConfigDto?>(new SosClusterGroupingConfigDto
            {
                MaximumDistanceKm = maximumDistanceKm.Value,
                UpdatedAt = DateTime.UtcNow
            });
        }

        public Task<SosClusterGroupingConfigDto> UpsertAsync(double maximumDistanceKm, Guid updatedBy, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
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
