using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.Emergency;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Entities.Personnel;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.System;

namespace RESQ.Tests.Infrastructure.System;

public class ServiceZoneSummaryRepositoryTests
{
    [Fact]
    public async Task GetResourceCountsAsync_CountsOnlyRowsInsidePolygon_AndSeparatesResourceTypes()
    {
        await using var context = CreateContext();

        context.SosRequests.AddRange(
            CreateSosRequest(1, 10.5, 106.5, SosRequestStatus.Pending),
            CreateSosRequest(2, 10.6, 106.6, SosRequestStatus.Incident),
            CreateSosRequest(3, 10.7, 106.7, SosRequestStatus.Assigned),
            CreateSosRequest(4, 12.0, 108.0, SosRequestStatus.Pending),
            CreateSosRequest(5, null, null, SosRequestStatus.Incident));

        context.TeamIncidents.AddRange(
            new TeamIncident { Id = 1, Location = CreatePoint(10.5, 106.5), Status = "Resolved" },
            new TeamIncident { Id = 2, Location = CreatePoint(12.0, 108.0), Status = "Reported" },
            new TeamIncident { Id = 3, Location = null, Status = "Reported" });

        context.AssemblyPoints.AddRange(
            new AssemblyPoint { Id = 1, Code = "AP-1", Name = "Assembly inside", Location = CreatePoint(10.8, 106.8), Status = "Closed" },
            new AssemblyPoint { Id = 2, Code = "AP-2", Name = "Assembly boundary", Location = CreatePoint(10.0, 106.5), Status = "Active" },
            new AssemblyPoint { Id = 3, Code = "AP-3", Name = "Assembly outside", Location = CreatePoint(12.0, 108.0), Status = "Active" });

        context.Depots.AddRange(
            new Depot { Id = 1, Name = "Depot inside", Location = CreatePoint(10.4, 106.4), Status = "Inactive" },
            new Depot { Id = 2, Name = "Depot outside", Location = CreatePoint(12.0, 108.0), Status = "Active" },
            new Depot { Id = 3, Name = "Depot null", Location = null, Status = "Active" });

        await context.SaveChangesAsync();

        var repository = CreateRepository(context);

        var result = await repository.GetResourceCountsAsync([BuildZone(1)]);

        var counts = Assert.Single(result).Value;
        Assert.Equal(1, counts.PendingSosRequestCount);
        Assert.Equal(1, counts.IncidentSosRequestCount);
        Assert.Equal(1, counts.TeamIncidentCount);
        Assert.Equal(2, counts.AssemblyPointCount);
        Assert.Equal(1, counts.DepotCount);
    }

    [Fact]
    public async Task GetResourceCountsAsync_ExcludesNonPendingAndNonIncidentSosStatuses()
    {
        await using var context = CreateContext();

        context.SosRequests.AddRange(
            CreateSosRequest(1, 10.1, 106.1, SosRequestStatus.Assigned),
            CreateSosRequest(2, 10.2, 106.2, SosRequestStatus.InProgress),
            CreateSosRequest(3, 10.3, 106.3, SosRequestStatus.Resolved),
            CreateSosRequest(4, 10.4, 106.4, SosRequestStatus.Cancelled));

        await context.SaveChangesAsync();

        var repository = CreateRepository(context);

        var result = await repository.GetResourceCountsAsync([BuildZone(1)]);

        var counts = Assert.Single(result).Value;
        Assert.Equal(0, counts.PendingSosRequestCount);
        Assert.Equal(0, counts.IncidentSosRequestCount);
    }

    [Fact]
    public async Task GetResourceCountsAsync_InvalidPolygon_ReturnsZeroCounts()
    {
        await using var context = CreateContext();

        context.SosRequests.Add(CreateSosRequest(1, 10.5, 106.5, SosRequestStatus.Pending));
        context.TeamIncidents.Add(new TeamIncident { Id = 1, Location = CreatePoint(10.5, 106.5), Status = "Reported" });
        context.AssemblyPoints.Add(new AssemblyPoint { Id = 1, Code = "AP-1", Name = "Assembly", Location = CreatePoint(10.5, 106.5) });
        context.Depots.Add(new Depot { Id = 1, Name = "Depot", Location = CreatePoint(10.5, 106.5), Status = "Active" });
        await context.SaveChangesAsync();

        var repository = CreateRepository(context);
        var invalidZone = BuildZone(1);
        invalidZone.Coordinates = invalidZone.Coordinates.Take(2).ToList();

        var result = await repository.GetResourceCountsAsync([invalidZone]);

        var counts = Assert.Single(result).Value;
        Assert.Equal(0, counts.PendingSosRequestCount);
        Assert.Equal(0, counts.IncidentSosRequestCount);
        Assert.Equal(0, counts.TeamIncidentCount);
        Assert.Equal(0, counts.AssemblyPointCount);
        Assert.Equal(0, counts.DepotCount);
    }

    private static ServiceZoneModel BuildZone(int id) =>
        new()
        {
            Id = id,
            Name = $"Zone {id}",
            Coordinates =
            [
                new CoordinatePoint { Latitude = 10.0, Longitude = 106.0 },
                new CoordinatePoint { Latitude = 10.0, Longitude = 107.0 },
                new CoordinatePoint { Latitude = 11.0, Longitude = 107.0 },
                new CoordinatePoint { Latitude = 11.0, Longitude = 106.0 }
            ]
        };

    private static SosRequest CreateSosRequest(
        int id,
        double? latitude,
        double? longitude,
        SosRequestStatus status)
    {
        return new SosRequest
        {
            Id = id,
            Status = status.ToString(),
            RawMessage = $"SOS {id}",
            CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            Location = latitude.HasValue && longitude.HasValue
                ? CreatePoint(latitude.Value, longitude.Value)
                : null
        };
    }

    private static Point CreatePoint(double latitude, double longitude) =>
        new(longitude, latitude) { SRID = 4326 };

    private static ResQDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ResQDbContext(options);
    }

    private static ServiceZoneSummaryRepository CreateRepository(ResQDbContext context)
    {
        var unitOfWork = new UnitOfWork(context, NullLogger<UnitOfWork>.Instance);
        return new ServiceZoneSummaryRepository(unitOfWork);
    }
}
