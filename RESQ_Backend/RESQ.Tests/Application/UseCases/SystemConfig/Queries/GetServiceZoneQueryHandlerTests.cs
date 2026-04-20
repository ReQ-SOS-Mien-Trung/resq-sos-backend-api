using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.SystemConfig.Queries.GetServiceZone;
using RESQ.Domain.Entities.System;

namespace RESQ.Tests.Application.UseCases.SystemConfig.Queries;

public class GetServiceZoneQueryHandlerTests
{
    [Fact]
    public async Task Handle_GetAllServiceZones_AddsCountsForEachZone()
    {
        var zones = new List<ServiceZoneModel>
        {
            BuildZone(1, "Zone A"),
            BuildZone(2, "Zone B")
        };
        var serviceZoneRepository = new StubServiceZoneRepository { AllZones = zones };
        var summaryRepository = new StubServiceZoneSummaryRepository(new Dictionary<int, ServiceZoneResourceCounts>
        {
            [1] = new(1, PendingSosRequestCount: 7, IncidentSosRequestCount: 3, TeamIncidentCount: 2, AssemblyPointCount: 4, DepotCount: 5),
            [2] = new(2, PendingSosRequestCount: 1, IncidentSosRequestCount: 0, TeamIncidentCount: 6, AssemblyPointCount: 8, DepotCount: 9)
        });
        var handler = new GetServiceZoneQueryHandler(serviceZoneRepository, summaryRepository);

        var result = await handler.Handle(new GetAllServiceZoneQuery(), CancellationToken.None);

        Assert.Equal([1, 2], summaryRepository.LastServiceZoneIds);
        Assert.Collection(
            result,
            zone =>
            {
                Assert.Equal(7, zone.Counts.PendingSosRequestCount);
                Assert.Equal(3, zone.Counts.IncidentSosRequestCount);
                Assert.Equal(2, zone.Counts.TeamIncidentCount);
                Assert.Equal(4, zone.Counts.AssemblyPointCount);
                Assert.Equal(5, zone.Counts.DepotCount);
            },
            zone =>
            {
                Assert.Equal(1, zone.Counts.PendingSosRequestCount);
                Assert.Equal(0, zone.Counts.IncidentSosRequestCount);
                Assert.Equal(6, zone.Counts.TeamIncidentCount);
                Assert.Equal(8, zone.Counts.AssemblyPointCount);
                Assert.Equal(9, zone.Counts.DepotCount);
            });
    }

    [Fact]
    public async Task Handle_GetActiveServiceZones_UsesActiveZonesAndAddsCounts()
    {
        var activeZone = BuildZone(10, "Active Zone");
        var serviceZoneRepository = new StubServiceZoneRepository
        {
            ActiveZones = [activeZone],
            AllZones = [activeZone, BuildZone(20, "Inactive Zone")]
        };
        var summaryRepository = new StubServiceZoneSummaryRepository(new Dictionary<int, ServiceZoneResourceCounts>
        {
            [10] = new(10, PendingSosRequestCount: 2, IncidentSosRequestCount: 1, TeamIncidentCount: 3, AssemblyPointCount: 4, DepotCount: 5)
        });
        var handler = new GetServiceZoneQueryHandler(serviceZoneRepository, summaryRepository);

        var result = await handler.Handle(new GetServiceZoneQuery(), CancellationToken.None);

        var zone = Assert.Single(result);
        Assert.Equal(10, zone.Id);
        Assert.Equal([10], summaryRepository.LastServiceZoneIds);
        Assert.Equal(2, zone.Counts.PendingSosRequestCount);
        Assert.Equal(1, zone.Counts.IncidentSosRequestCount);
    }

    [Fact]
    public async Task Handle_GetServiceZoneById_SeparatesPendingAndIncidentSosCounts()
    {
        var zone = BuildZone(12, "Zone Detail");
        var serviceZoneRepository = new StubServiceZoneRepository { ZoneById = zone };
        var summaryRepository = new StubServiceZoneSummaryRepository(new Dictionary<int, ServiceZoneResourceCounts>
        {
            [12] = new(12, PendingSosRequestCount: 11, IncidentSosRequestCount: 4, TeamIncidentCount: 0, AssemblyPointCount: 0, DepotCount: 0)
        });
        var handler = new GetServiceZoneQueryHandler(serviceZoneRepository, summaryRepository);

        var result = await handler.Handle(new GetServiceZoneByIdQuery(12), CancellationToken.None);

        Assert.Equal([12], summaryRepository.LastServiceZoneIds);
        Assert.Equal(11, result.Counts.PendingSosRequestCount);
        Assert.Equal(4, result.Counts.IncidentSosRequestCount);
    }

    [Fact]
    public async Task Handle_WhenSummaryReturnsNoCounts_DefaultsToZeroCounts()
    {
        var zone = BuildZone(1, "Invalid Polygon Zone");
        var serviceZoneRepository = new StubServiceZoneRepository { AllZones = [zone] };
        var summaryRepository = new StubServiceZoneSummaryRepository(new Dictionary<int, ServiceZoneResourceCounts>());
        var handler = new GetServiceZoneQueryHandler(serviceZoneRepository, summaryRepository);

        var result = await handler.Handle(new GetAllServiceZoneQuery(), CancellationToken.None);

        var response = Assert.Single(result);
        Assert.Equal(0, response.Counts.PendingSosRequestCount);
        Assert.Equal(0, response.Counts.IncidentSosRequestCount);
        Assert.Equal(0, response.Counts.TeamIncidentCount);
        Assert.Equal(0, response.Counts.AssemblyPointCount);
        Assert.Equal(0, response.Counts.DepotCount);
    }

    private static ServiceZoneModel BuildZone(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Coordinates =
            [
                new CoordinatePoint { Latitude = 10, Longitude = 106 },
                new CoordinatePoint { Latitude = 10, Longitude = 107 },
                new CoordinatePoint { Latitude = 11, Longitude = 107 },
                new CoordinatePoint { Latitude = 11, Longitude = 106 }
            ],
            IsActive = true,
            CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    private sealed class StubServiceZoneRepository : IServiceZoneRepository
    {
        public List<ServiceZoneModel> ActiveZones { get; init; } = [];
        public List<ServiceZoneModel> AllZones { get; init; } = [];
        public ServiceZoneModel? ZoneById { get; init; }

        public Task<ServiceZoneModel?> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ActiveZones.FirstOrDefault());

        public Task<List<ServiceZoneModel>> GetAllActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ActiveZones);

        public Task<ServiceZoneModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(ZoneById?.Id == id ? ZoneById : null);

        public Task<List<ServiceZoneModel>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(AllZones);

        public Task CreateAsync(ServiceZoneModel model, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(ServiceZoneModel model, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeactivateAllExceptAsync(int excludeId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> IsLocationInServiceZoneAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class StubServiceZoneSummaryRepository(
        IReadOnlyDictionary<int, ServiceZoneResourceCounts> counts)
        : IServiceZoneSummaryRepository
    {
        public int[] LastServiceZoneIds { get; private set; } = [];

        public Task<IReadOnlyDictionary<int, ServiceZoneResourceCounts>> GetResourceCountsAsync(
            IReadOnlyCollection<ServiceZoneModel> serviceZones,
            CancellationToken cancellationToken = default)
        {
            LastServiceZoneIds = serviceZones.Select(x => x.Id).ToArray();
            return Task.FromResult(counts);
        }
    }
}
