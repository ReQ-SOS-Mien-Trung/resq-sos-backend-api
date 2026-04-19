using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Emergency;

namespace RESQ.Tests.Infrastructure.Emergency;

public class SosClusterRepositoryTests
{
    [Fact]
    public async Task GetPagedAsync_FiltersBySosRequestId_AndKeepsLinkedSosRequestIds()
    {
        await using var context = CreateContext();

        context.SosClusters.AddRange(
            new SosCluster
            {
                Id = 1,
                Status = "Pending",
                CreatedAt = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc)
            },
            new SosCluster
            {
                Id = 2,
                Status = "Pending",
                CreatedAt = new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc)
            });

        context.SosRequests.AddRange(
            new SosRequest { Id = 101, ClusterId = 1, Status = "Pending", RawMessage = "SOS 101" },
            new SosRequest { Id = 102, ClusterId = 1, Status = "Pending", RawMessage = "SOS 102" },
            new SosRequest { Id = 201, ClusterId = 2, Status = "Pending", RawMessage = "SOS 201" });

        await context.SaveChangesAsync();

        var repository = CreateRepository(context);

        var result = await repository.GetPagedAsync(pageNumber: 1, pageSize: 10, sosRequestId: 201);

        var cluster = Assert.Single(result.Items);
        Assert.Equal(2, cluster.Id);
        Assert.Equal([201], cluster.SosRequestIds);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_OrdersByCreatedAtDescending_AndAppliesPagination()
    {
        await using var context = CreateContext();

        context.SosClusters.AddRange(
            new SosCluster
            {
                Id = 1,
                Status = "Pending",
                CreatedAt = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc)
            },
            new SosCluster
            {
                Id = 2,
                Status = "Pending",
                CreatedAt = new DateTime(2026, 4, 3, 8, 0, 0, DateTimeKind.Utc)
            },
            new SosCluster
            {
                Id = 3,
                Status = "Pending",
                CreatedAt = new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc)
            });

        context.SosRequests.AddRange(
            new SosRequest { Id = 101, ClusterId = 1, Status = "Pending", RawMessage = "SOS 101" },
            new SosRequest { Id = 201, ClusterId = 2, Status = "Pending", RawMessage = "SOS 201" },
            new SosRequest { Id = 301, ClusterId = 3, Status = "Pending", RawMessage = "SOS 301" });

        await context.SaveChangesAsync();

        var repository = CreateRepository(context);

        var result = await repository.GetPagedAsync(pageNumber: 2, pageSize: 1);

        var cluster = Assert.Single(result.Items);
        Assert.Equal(3, cluster.Id);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(1, result.PageSize);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCluster()
    {
        await using var context = CreateContext();

        context.SosClusters.Add(new SosCluster
        {
            Id = 9,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var repository = CreateRepository(context);

        await repository.DeleteAsync(9);
        await context.SaveChangesAsync();

        Assert.Null(await context.SosClusters.FindAsync(9));
    }

    private static ResQDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ResQDbContext(options);
    }

    private static SosClusterRepository CreateRepository(ResQDbContext context)
    {
        var unitOfWork = new UnitOfWork(context, NullLogger<UnitOfWork>.Instance);
        return new SosClusterRepository(unitOfWork);
    }
}
