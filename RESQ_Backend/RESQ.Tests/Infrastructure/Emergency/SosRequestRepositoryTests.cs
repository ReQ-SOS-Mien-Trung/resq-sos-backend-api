using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using RESQ.Domain.Enum.Emergency;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Emergency;

namespace RESQ.Tests.Infrastructure.Emergency;

public class SosRequestRepositoryTests
{
    [Fact]
    public async Task GetByBoundsAsync_ExcludesSosRequestsWithoutLocation()
    {
        await using var context = CreateContext();

        context.SosRequests.AddRange(
            CreateSosRequestEntity(1, 10.75, 106.66, "Pending", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
            CreateSosRequestEntity(2, null, null, "Pending", new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc)));

        await context.SaveChangesAsync();

        var repository = CreateRepository(context);

        var result = await repository.GetByBoundsAsync(10.70, 10.80, 106.60, 106.70);

        var dto = Assert.Single(result);
        Assert.Equal(1, dto.Id);
    }

    [Fact]
    public async Task GetByBoundsAsync_FiltersByBoundsCorrectly()
    {
        await using var context = CreateContext();

        context.SosRequests.AddRange(
            CreateSosRequestEntity(1, 10.75, 106.66, "Pending", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
            CreateSosRequestEntity(2, 10.79, 106.69, "Pending", new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc)),
            CreateSosRequestEntity(3, 11.20, 107.10, "Pending", new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc)));

        await context.SaveChangesAsync();

        var repository = CreateRepository(context);

        var result = await repository.GetByBoundsAsync(10.70, 10.80, 106.60, 106.70);

        Assert.Equal([2, 1], result.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task GetByBoundsAsync_FiltersByStatusCorrectly()
    {
        await using var context = CreateContext();

        context.SosRequests.AddRange(
            CreateSosRequestEntity(1, 10.75, 106.66, "Pending", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
            CreateSosRequestEntity(2, 10.76, 106.67, "Assigned", new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc)),
            CreateSosRequestEntity(3, 10.77, 106.68, "Resolved", new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc)));

        await context.SaveChangesAsync();

        var repository = CreateRepository(context);

        var result = await repository.GetByBoundsAsync(
            10.70,
            10.80,
            106.60,
            106.70,
            [SosRequestStatus.Pending, SosRequestStatus.Assigned]);

        Assert.Equal([2, 1], result.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task GetByBoundsAsync_FiltersByPriorityAndSosType()
    {
        await using var context = CreateContext();

        context.SosRequests.AddRange(
            CreateSosRequestEntity(1, 10.75, 106.66, "Pending", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), priorityLevel: "High", sosType: "Rescue"),
            CreateSosRequestEntity(2, 10.76, 106.67, "Pending", new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc), priorityLevel: "Critical", sosType: "Relief"),
            CreateSosRequestEntity(3, 10.77, 106.68, "Pending", new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc), priorityLevel: "High", sosType: "Relief"));

        await context.SaveChangesAsync();

        var repository = CreateRepository(context);

        var result = await repository.GetByBoundsAsync(
            10.70,
            10.80,
            106.60,
            106.70,
            priorities: [SosPriorityLevel.High],
            sosTypes: [SosRequestType.Relief]);

        Assert.Equal([3], result.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task GetByBoundsAsync_OrdersByCreatedAtDescending()
    {
        await using var context = CreateContext();

        context.SosRequests.AddRange(
            CreateSosRequestEntity(1, 10.75, 106.66, "Pending", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
            CreateSosRequestEntity(2, 10.76, 106.67, "Pending", new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc)),
            CreateSosRequestEntity(3, 10.77, 106.68, "Pending", new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc)));

        await context.SaveChangesAsync();

        var repository = CreateRepository(context);

        var result = await repository.GetByBoundsAsync(10.70, 10.80, 106.60, 106.70);

        Assert.Equal([2, 3, 1], result.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task GetAllPagedAsync_FiltersByStatusesBeforePagination()
    {
        await using var context = CreateContext();

        context.SosRequests.AddRange(
            CreateSosRequestEntity(1, 10.75, 106.66, "Pending", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
            CreateSosRequestEntity(2, 10.76, 106.67, "Assigned", new DateTime(2026, 4, 4, 0, 0, 0, DateTimeKind.Utc)),
            CreateSosRequestEntity(3, 10.77, 106.68, "Assigned", new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc)),
            CreateSosRequestEntity(4, 10.78, 106.69, "Resolved", new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc)));

        await context.SaveChangesAsync();

        var repository = CreateRepository(context);

        var result = await repository.GetAllPagedAsync(
            pageNumber: 2,
            pageSize: 1,
            statuses: [SosRequestStatus.Assigned]);

        var sos = Assert.Single(result.Items);
        Assert.Equal(3, sos.Id);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(1, result.PageSize);
    }

    [Fact]
    public async Task GetAllPagedAsync_FiltersByStatusPriorityAndSosTypeBeforePagination()
    {
        await using var context = CreateContext();

        context.SosRequests.AddRange(
            CreateSosRequestEntity(1, 10.75, 106.66, "Assigned", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), priorityLevel: "High", sosType: "Rescue"),
            CreateSosRequestEntity(2, 10.76, 106.67, "Assigned", new DateTime(2026, 4, 4, 0, 0, 0, DateTimeKind.Utc), priorityLevel: "Critical", sosType: "Rescue"),
            CreateSosRequestEntity(3, 10.77, 106.68, "Assigned", new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc), priorityLevel: "Critical", sosType: "Relief"),
            CreateSosRequestEntity(4, 10.78, 106.69, "Resolved", new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc), priorityLevel: "Critical", sosType: "Rescue"));

        await context.SaveChangesAsync();

        var repository = CreateRepository(context);

        var result = await repository.GetAllPagedAsync(
            pageNumber: 1,
            pageSize: 10,
            statuses: [SosRequestStatus.Assigned],
            priorities: [SosPriorityLevel.Critical],
            sosTypes: [SosRequestType.Rescue]);

        var sos = Assert.Single(result.Items);
        Assert.Equal(2, sos.Id);
        Assert.Equal(1, result.TotalCount);
    }

    private static SosRequest CreateSosRequestEntity(
        int id,
        double? latitude,
        double? longitude,
        string status,
        DateTime createdAtUtc,
        string? priorityLevel = null,
        string? sosType = null)
    {
        return new SosRequest
        {
            Id = id,
            UserId = Guid.NewGuid(),
            RawMessage = $"SOS {id}",
            SosType = sosType,
            PriorityLevel = priorityLevel,
            Status = status,
            CreatedAt = createdAtUtc,
            LastUpdatedAt = createdAtUtc,
            ReceivedAt = createdAtUtc,
            Location = latitude.HasValue && longitude.HasValue
                ? new Point(longitude.Value, latitude.Value) { SRID = 4326 }
                : null
        };
    }

    private static ResQDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ResQDbContext(options);
    }

    private static SosRequestRepository CreateRepository(ResQDbContext context)
    {
        var unitOfWork = new UnitOfWork(context, NullLogger<UnitOfWork>.Instance);
        return new SosRequestRepository(unitOfWork);
    }
}
