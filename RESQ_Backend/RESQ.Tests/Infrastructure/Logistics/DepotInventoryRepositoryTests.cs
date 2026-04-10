using Microsoft.EntityFrameworkCore;
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
    public async Task ConsumeReservedSuppliesAsync_WithConsumableItem_DoesNotRequireReusableUnits()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ResQDbContext(options);
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

    private sealed class StubInventoryQueryService : IInventoryQueryService
    {
        public InventoryAvailability ComputeAvailability(int? quantity, int? missionReserved, int? transferReserved) =>
            new(quantity ?? 0, missionReserved ?? 0, transferReserved ?? 0);
    }
}
