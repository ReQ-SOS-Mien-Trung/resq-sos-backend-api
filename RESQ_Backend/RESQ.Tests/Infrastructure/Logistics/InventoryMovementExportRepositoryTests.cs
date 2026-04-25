using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Logistics;

namespace RESQ.Tests.Infrastructure.Logistics;

public class InventoryMovementExportRepositoryTests
{
    [Fact]
    public async Task GetMovementRowsAsync_DoesNotLeakReusableTransferOutIntoTargetDepotExport()
    {
        await using var context = CreateContext();
        SeedReusableTransferLogs(context);

        var repository = CreateRepository(context);
        var period = InventoryMovementExportPeriod.ForDateRange(
            new DateOnly(2026, 4, 21),
            new DateOnly(2026, 4, 21));

        var targetDepotRows = await repository.GetMovementRowsAsync(period, 2, null);
        var sourceDepotRows = await repository.GetMovementRowsAsync(period, 1, null);

        Assert.Single(targetDepotRows);
        Assert.Equal("MTT-001", targetDepotRows[0].SerialNumber);
        Assert.Equal(1, targetDepotRows[0].QuantityChange);

        Assert.Single(sourceDepotRows);
        Assert.Equal("MTT-001", sourceDepotRows[0].SerialNumber);
        Assert.Equal(-1, sourceDepotRows[0].QuantityChange);
    }

    private static ResQDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ResQDbContext(options);
    }

    private static InventoryMovementExportRepository CreateRepository(ResQDbContext context)
    {
        var unitOfWork = new UnitOfWork(context, NullLogger<UnitOfWork>.Instance);
        return new InventoryMovementExportRepository(unitOfWork);
    }

    private static void SeedReusableTransferLogs(ResQDbContext context)
    {
        context.Categories.Add(new Category
        {
            Id = 51,
            Code = "Medical",
            Name = "Medical"
        });

        context.Depots.AddRange(
            new Depot
            {
                Id = 1,
                Name = "Kho Nguon",
                Status = "Available"
            },
            new Depot
            {
                Id = 2,
                Name = "Kho Dich",
                Status = "Available"
            });

        context.ItemModels.Add(new ItemModel
        {
            Id = 501,
            CategoryId = 51,
            Name = "May tro tho",
            Unit = "cai",
            ItemType = "Reusable"
        });

        context.DepotSupplyRequests.Add(new DepotSupplyRequest
        {
            Id = 7001,
            SourceDepotId = 1,
            RequestingDepotId = 2,
            RequestedBy = Guid.Parse("cccccccc-1111-1111-1111-111111111111"),
            CreatedAt = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)
        });

        context.ReusableItems.Add(new ReusableItem
        {
            Id = 5001,
            DepotId = 2,
            ItemModelId = 501,
            Status = "Available",
            Condition = "Good",
            SerialNumber = "MTT-001",
            CreatedAt = new DateTime(2026, 4, 20, 7, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 4, 21, 7, 0, 0, DateTimeKind.Utc)
        });

        context.InventoryLogs.AddRange(
            new InventoryLog
            {
                Id = 8001,
                ReusableItemId = 5001,
                ActionType = "TransferOut",
                QuantityChange = -1,
                SourceType = "Transfer",
                SourceId = 7001,
                CreatedAt = new DateTime(2026, 4, 21, 7, 0, 0, DateTimeKind.Utc)
            },
            new InventoryLog
            {
                Id = 8002,
                ReusableItemId = 5001,
                ActionType = "TransferIn",
                QuantityChange = 1,
                SourceType = "Transfer",
                SourceId = 7001,
                CreatedAt = new DateTime(2026, 4, 21, 8, 0, 0, DateTimeKind.Utc)
            });

        context.SaveChanges();
    }
}
