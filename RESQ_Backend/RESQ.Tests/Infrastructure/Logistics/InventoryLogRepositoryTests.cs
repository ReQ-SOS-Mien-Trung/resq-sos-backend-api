using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Logistics;

namespace RESQ.Tests.Infrastructure.Logistics;

public class InventoryLogRepositoryTests
{
    [Fact]
    public async Task GetTransactionHistoryAsync_SeparatesMaintenanceLogsCreatedAtDifferentTimesOnSameDay()
    {
        await using var context = CreateContext();
        SeedMaintenanceLogs(context);

        var repository = CreateRepository(context);

        var result = await repository.GetTransactionHistoryAsync(
            depotId: 3,
            actionTypes: null,
            sourceTypes: null,
            fromDate: null,
            toDate: null,
            pageNumber: 1,
            pageSize: 10);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, transaction => Assert.Single(transaction.Items));
    }

    private static ResQDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ResQDbContext(options);
    }

    private static InventoryLogRepository CreateRepository(ResQDbContext context)
    {
        var unitOfWork = new UnitOfWork(context, NullLogger<UnitOfWork>.Instance);
        return new InventoryLogRepository(unitOfWork);
    }

    private static void SeedMaintenanceLogs(ResQDbContext context)
    {
        context.Categories.Add(new Category
        {
            Id = 40,
            Code = "Equipment",
            Name = "Equipment"
        });

        context.Depots.Add(new Depot
        {
            Id = 3,
            Name = "Kho HCM",
            Status = "Available"
        });

        context.ItemModels.Add(new ItemModel
        {
            Id = 401,
            CategoryId = 40,
            Name = "Bo dam",
            Unit = "cai",
            ItemType = "Reusable"
        });

        context.ReusableItems.AddRange(
            new ReusableItem
            {
                Id = 4001,
                DepotId = 3,
                ItemModelId = 401,
                Status = "Maintenance",
                Condition = "Good",
                SerialNumber = "BD-001",
                CreatedAt = new DateTime(2026, 4, 20, 7, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 4, 21, 1, 0, 0, DateTimeKind.Utc)
            },
            new ReusableItem
            {
                Id = 4002,
                DepotId = 3,
                ItemModelId = 401,
                Status = "Maintenance",
                Condition = "Good",
                SerialNumber = "BD-002",
                CreatedAt = new DateTime(2026, 4, 20, 7, 5, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 4, 21, 2, 0, 0, DateTimeKind.Utc)
            });

        context.InventoryLogs.AddRange(
            new InventoryLog
            {
                Id = 5001,
                ReusableItemId = 4001,
                ActionType = "Adjust",
                QuantityChange = 0,
                SourceType = "Maintenance",
                PerformedBy = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"),
                Note = "Bao tri bo dam 1",
                CreatedAt = new DateTime(2026, 4, 21, 1, 0, 0, DateTimeKind.Utc)
            },
            new InventoryLog
            {
                Id = 5002,
                ReusableItemId = 4002,
                ActionType = "Adjust",
                QuantityChange = 0,
                SourceType = "Maintenance",
                PerformedBy = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"),
                Note = "Bao tri bo dam 2",
                CreatedAt = new DateTime(2026, 4, 21, 2, 0, 0, DateTimeKind.Utc)
            });

        context.SaveChanges();
    }
}
