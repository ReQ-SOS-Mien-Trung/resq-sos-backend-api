using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Domain.Entities.Logistics.Services;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Logistics;

namespace RESQ.Tests.Infrastructure.Logistics;

public class DepotInventoryRepositoryTests
{
    [Fact]
    public async Task SearchForAgentAsync_ReturnsMedicalItems_ForGenericVietnameseMedicineKeyword()
    {
        await using var context = CreateContext();
        SeedDepotForAgentSearch(context);

        var repository = CreateRepository(context);

        var (items, totalCount) = await repository.SearchForAgentAsync(
            categoryKeyword: "Y tế",
            typeKeyword: "Thuốc men",
            page: 1,
            pageSize: 10,
            allowedDepotIds: [3]);

        Assert.True(totalCount > 0);
        Assert.Contains(items, item => item.ItemId == 3 && item.ItemName.Contains("Paracetamol", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(items, item => item.ItemName.Contains("Chăn", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchForAgentAsync_ReturnsHeatingItems_WhenBlanketIsSearchedThroughClothingTerms()
    {
        await using var context = CreateContext();
        SeedDepotForAgentSearch(context);

        var repository = CreateRepository(context);

        var (items, totalCount) = await repository.SearchForAgentAsync(
            categoryKeyword: "Quần áo",
            typeKeyword: "Chăn màn",
            page: 1,
            pageSize: 10,
            allowedDepotIds: [3]);

        Assert.True(totalCount > 0);
        Assert.Contains(items, item => item.ItemId == 6 && item.ItemName.Contains("Chăn", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ConsumeReservedSuppliesAsync_WithConsumableItem_DoesNotRequireReusableUnits()
    {
        await using var context = CreateContext();
        context.ItemModels.Add(new ItemModel
        {
            Id = 27,
            Name = "Bang gac y te vo khuan",
            Unit = "cuon",
            ItemType = "Consumable"
        });
        context.SupplyInventories.Add(new SupplyInventory
        {
            Id = 1,
            DepotId = 3,
            ItemModelId = 27,
            Quantity = 20,
            MissionReservedQuantity = 11,
            TransferReservedQuantity = 0
        });
        context.SupplyInventoryLots.Add(new SupplyInventoryLot
        {
            Id = 101,
            SupplyInventoryId = 1,
            Quantity = 20,
            RemainingQuantity = 20,
            ReceivedDate = DateTime.UtcNow.AddDays(-2),
            ExpiredDate = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        });
        await context.SaveChangesAsync();

        var unitOfWork = new UnitOfWork(context, NullLogger<UnitOfWork>.Instance);
        var repository = new DepotInventoryRepository(unitOfWork, new StubInventoryQueryService());

        var result = await repository.ConsumeReservedSuppliesAsync(
            depotId: 3,
            items: [(27, 11)],
            performedBy: Guid.NewGuid(),
            activityId: 13,
            missionId: 7);

        var inventory = await context.SupplyInventories.SingleAsync(x => x.Id == 1);
        var lot = await context.SupplyInventoryLots.SingleAsync(x => x.Id == 101);
        var executionItem = Assert.Single(result.Items);

        Assert.Equal(27, executionItem.ItemModelId);
        Assert.Equal(11, executionItem.Quantity);
        Assert.Empty(executionItem.ReusableUnits);
        Assert.Single(executionItem.LotAllocations);
        Assert.Equal(9, inventory.Quantity);
        Assert.Equal(0, inventory.MissionReservedQuantity);
        Assert.Equal(9, lot.RemainingQuantity);
    }

    [Fact]
    public async Task GetLotDetailedInventoryForClosureAsync_WithReusableItems_ReturnsRowsPerReusableUnit()
    {
        await using var context = CreateContext();
        context.Categories.Add(new Category { Id = 1, Code = "OTHERS", Name = "Khác" });
        context.Depots.Add(new Depot
        {
            Id = 1,
            Name = "Kho Huế",
            Status = "Available"
        });
        context.ItemModels.Add(new ItemModel
        {
            Id = 108,
            CategoryId = 1,
            Name = "Bộ đèn pin đội đầu",
            Unit = "bộ",
            ItemType = "Reusable"
        });

        var firstReceivedAt = new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc);
        var secondReceivedAt = firstReceivedAt.AddDays(4);
        context.ReusableItems.AddRange(
            new ReusableItem
            {
                Id = 1,
                DepotId = 1,
                ItemModelId = 108,
                SerialNumber = "HEADLAMP-001",
                Status = "Available",
                Condition = "Good",
                CreatedAt = firstReceivedAt,
                UpdatedAt = firstReceivedAt
            },
            new ReusableItem
            {
                Id = 2,
                DepotId = 1,
                ItemModelId = 108,
                SerialNumber = "HEADLAMP-002",
                Status = "Available",
                Condition = "Good",
                CreatedAt = secondReceivedAt,
                UpdatedAt = secondReceivedAt
            });
        await context.SaveChangesAsync();

        var unitOfWork = new UnitOfWork(context, NullLogger<UnitOfWork>.Instance);
        var repository = new DepotRepository(unitOfWork, context);

        var result = await repository.GetLotDetailedInventoryForClosureAsync(1);

        Assert.Equal(2, result.Count);

        var firstRow = result[0];
        Assert.Equal("Reusable", firstRow.ItemType);
        Assert.Equal(1, firstRow.Quantity);
        Assert.Null(firstRow.LotId);
        Assert.Equal(1, firstRow.ReusableItemId);
        Assert.Equal("HEADLAMP-001", firstRow.SerialNumber);
        Assert.Equal(firstReceivedAt, firstRow.ReceivedDate);

        var secondRow = result[1];
        Assert.Equal(2, secondRow.ReusableItemId);
        Assert.Equal("HEADLAMP-002", secondRow.SerialNumber);
        Assert.Equal(secondReceivedAt, secondRow.ReceivedDate);
    }

    private static ResQDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ResQDbContext(options);
    }

    private static DepotInventoryRepository CreateRepository(ResQDbContext context)
    {
        var unitOfWork = new UnitOfWork(context, NullLogger<UnitOfWork>.Instance);
        return new DepotInventoryRepository(unitOfWork, new StubInventoryQueryService());
    }

    private static void SeedDepotForAgentSearch(ResQDbContext context)
    {
        context.Categories.AddRange(
            new Category { Id = 3, Code = "Medical", Name = "Y tế" },
            new Category { Id = 5, Code = "Clothing", Name = "Quần áo" },
            new Category { Id = 9, Code = "Heating", Name = "Sưởi ấm" });

        context.Depots.Add(new Depot
        {
            Id = 3,
            Name = "Kho Huế",
            Status = "Available"
        });

        context.ItemModels.AddRange(
            new ItemModel
            {
                Id = 3,
                CategoryId = 3,
                Name = "Thuốc hạ sốt Paracetamol 500mg",
                Unit = "viên",
                ItemType = "Consumable"
            },
            new ItemModel
            {
                Id = 6,
                CategoryId = 9,
                Name = "Chăn ấm giữ nhiệt",
                Unit = "chiếc",
                ItemType = "Consumable"
            });

        context.SupplyInventories.AddRange(
            new SupplyInventory
            {
                Id = 1,
                DepotId = 3,
                ItemModelId = 3,
                Quantity = 500,
                MissionReservedQuantity = 0,
                TransferReservedQuantity = 0
            },
            new SupplyInventory
            {
                Id = 2,
                DepotId = 3,
                ItemModelId = 6,
                Quantity = 25,
                MissionReservedQuantity = 0,
                TransferReservedQuantity = 0
            });

        context.SaveChanges();
    }
    private sealed class StubInventoryQueryService : IInventoryQueryService
    {
        public InventoryAvailability ComputeAvailability(int? quantity, int? missionReserved, int? transferReserved) =>
            new(quantity ?? 0, missionReserved ?? 0, transferReserved ?? 0);
    }
}
