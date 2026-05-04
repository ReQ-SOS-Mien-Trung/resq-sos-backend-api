using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Domain.Enum.Logistics;
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
            itemModelId: null,
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

    [Fact]
    public async Task GetInventoryLogsPagedAsync_GroupsMissionReturnActivityLogsAcrossReturnedAndLostItems()
    {
        await using var context = CreateContext();
        SeedMissionReturnActivityLogs(context);

        var repository = CreateRepository(context);

        var result = await repository.GetInventoryLogsPagedAsync(
            depotId: 3,
            itemModelId: null,
            actionTypes: null,
            sourceTypes: null,
            fromDate: null,
            toDate: null,
            search: null,
            pageNumber: 1,
            pageSize: 10);

        var log = Assert.Single(result.Items);

        Assert.Equal(nameof(InventoryActionType.Return), log.ActionType);
        Assert.Equal(nameof(InventorySourceType.Mission), log.SourceType);
        Assert.Equal(77, log.SourceId);
        Assert.Equal(1, log.QuantityChange);
        Assert.Null(log.ItemModelId);
        Assert.Equal(string.Empty, log.ItemModelName);
        Assert.Null(log.RemainingQuantity);

        Assert.Equal(2, log.LotDetails.Count);
        Assert.Contains(log.LotDetails, detail =>
            detail.ItemModelId == 101
            && detail.ActionType == nameof(InventoryActionType.Return)
            && detail.QuantityChange == 3
            && detail.Note == null);
        Assert.Contains(log.LotDetails, detail =>
            detail.ItemModelId == 101
            && detail.ActionType == nameof(InventoryActionType.Adjust)
            && detail.QuantityChange == -2
            && detail.Note == "Mất 2 bảng ca nhân");

        Assert.Equal(2, log.ReusableDetails.Count);
        Assert.Contains(log.ReusableDetails, detail =>
            detail.ItemModelId == 201
            && detail.ReusableItemId == 2001
            && detail.ActionType == nameof(InventoryActionType.Return)
            && detail.QuantityChange == 1
            && detail.Note == null);
        Assert.Contains(log.ReusableDetails, detail =>
            detail.ItemModelId == 201
            && detail.ReusableItemId == 2002
            && detail.ActionType == nameof(InventoryActionType.Adjust)
            && detail.QuantityChange == -1
            && detail.Note == "Mất bộ đầm BD-002");
    }

    [Fact]
    public async Task GetTransactionHistoryAsync_GroupsMissionReturnActivityLogsAcrossReturnedAndLostItems()
    {
        await using var context = CreateContext();
        SeedMissionReturnActivityLogs(context);

        var repository = CreateRepository(context);

        var result = await repository.GetTransactionHistoryAsync(
            depotId: 3,
            itemModelId: null,
            actionTypes: null,
            sourceTypes: null,
            fromDate: null,
            toDate: null,
            pageNumber: 1,
            pageSize: 10);

        var transaction = Assert.Single(result.Items);

        Assert.Equal(nameof(InventoryActionType.Return), transaction.ActionType);
        Assert.Equal(nameof(InventorySourceType.Mission), transaction.SourceType);
        Assert.Equal(77, transaction.SourceId);
        Assert.Equal("Trả thiếu bằng ca nhân", transaction.Note);
        Assert.Equal(4, transaction.Items.Count);
        Assert.Contains(transaction.Items, item =>
            item.ItemModelId == 101
            && item.ActionType == nameof(InventoryActionType.Return)
            && item.QuantityChange == 3
            && item.Note == null);
        Assert.Contains(transaction.Items, item =>
            item.ItemModelId == 101
            && item.ActionType == nameof(InventoryActionType.Adjust)
            && item.QuantityChange == -2
            && item.Note == "Mất 2 bảng ca nhân");
        Assert.Contains(transaction.Items, item =>
            item.ItemModelId == 201
            && item.ReusableItemId == 2001
            && item.ActionType == nameof(InventoryActionType.Return)
            && item.QuantityChange == 1
            && item.Note == null);
        Assert.Contains(transaction.Items, item =>
            item.ItemModelId == 201
            && item.ReusableItemId == 2002
            && item.ActionType == nameof(InventoryActionType.Adjust)
            && item.QuantityChange == -1
            && item.Note == "Mất bộ đầm BD-002");
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

    private static void SeedMissionReturnActivityLogs(ResQDbContext context)
    {
        var createdAt = new DateTime(2026, 4, 22, 3, 0, 0, DateTimeKind.Utc);

        context.Categories.Add(new Category
        {
            Id = 10,
            Code = "Medical",
            Name = "Medical"
        });

        context.Depots.Add(new Depot
        {
            Id = 3,
            Name = "Kho HCM",
            Status = "Available"
        });

        context.ItemModels.AddRange(
            new ItemModel
            {
                Id = 101,
                CategoryId = 10,
                Name = "Bang ca nhan",
                Unit = "goi",
                ItemType = "Consumable"
            },
            new ItemModel
            {
                Id = 201,
                CategoryId = 10,
                Name = "Bo dam",
                Unit = "cai",
                ItemType = "Reusable"
            });

        context.SupplyInventories.Add(new SupplyInventory
        {
            Id = 1001,
            DepotId = 3,
            ItemModelId = 101,
            Quantity = 20
        });

        context.SupplyInventoryLots.Add(new SupplyInventoryLot
        {
            Id = 3001,
            SupplyInventoryId = 1001,
            Quantity = 20,
            RemainingQuantity = 20,
            ReceivedDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        context.ReusableItems.AddRange(
            new ReusableItem
            {
                Id = 2001,
                DepotId = 3,
                ItemModelId = 201,
                Status = "Available",
                Condition = "Good",
                SerialNumber = "BD-001",
                CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new ReusableItem
            {
                Id = 2002,
                DepotId = 3,
                ItemModelId = 201,
                Status = "Lost",
                Condition = "Good",
                SerialNumber = "BD-002",
                CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
            });

        context.InventoryLogs.AddRange(
            new InventoryLog
            {
                Id = 6001,
                DepotSupplyInventoryId = 1001,
                SupplyInventoryLotId = 3001,
                ActionType = nameof(InventoryActionType.Return),
                QuantityChange = 3,
                SourceType = nameof(InventorySourceType.Mission),
                SourceId = 77,
                MissionId = 9,
                Note = "Trả thiếu bằng ca nhân",
                CreatedAt = createdAt
            },
            new InventoryLog
            {
                Id = 6002,
                DepotSupplyInventoryId = 1001,
                SupplyInventoryLotId = 3001,
                ActionType = nameof(InventoryActionType.Adjust),
                QuantityChange = -2,
                SourceType = nameof(InventorySourceType.Mission),
                SourceId = 77,
                MissionId = 9,
                Note = "Mất 2 bảng ca nhân",
                CreatedAt = createdAt.AddMilliseconds(100)
            },
            new InventoryLog
            {
                Id = 6003,
                ReusableItemId = 2001,
                ActionType = nameof(InventoryActionType.Return),
                QuantityChange = 1,
                SourceType = nameof(InventorySourceType.Mission),
                SourceId = 77,
                MissionId = 9,
                Note = "Bo dam tot",
                CreatedAt = createdAt.AddMilliseconds(200)
            },
            new InventoryLog
            {
                Id = 6004,
                ReusableItemId = 2002,
                ActionType = nameof(InventoryActionType.Adjust),
                QuantityChange = -1,
                SourceType = nameof(InventorySourceType.Mission),
                SourceId = 77,
                MissionId = 9,
                Note = "Mất bộ đầm BD-002",
                CreatedAt = createdAt.AddMilliseconds(300)
            });

        context.SaveChanges();
    }
}
