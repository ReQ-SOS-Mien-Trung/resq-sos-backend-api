using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
using RESQ.Application.UseCases.Logistics.Queries.GetClosureTransferSuggestions;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Tests.Application.UseCases.Logistics.Queries;

public class GetClosureTransferSuggestionsHandlerTests
{
    [Fact]
    public async Task Handle_PrefersNearestSingleDepot_AndConsolidatesAssignments()
    {
        var repository = new StubDepotRepository(
            sourceDepot: CreateDepot(10, "Kho nguồn", 10.0, 106.0, 1000m, 1000m),
            availableDepots:
            [
                CreateDepot(2, "Kho gần", 10.01, 106.01, 500m, 500m),
                CreateDepot(3, "Kho xa", 11.5, 108.0, 900m, 900m)
            ],
            inventoryItems:
            [
                CreateItem(101, "Gạo", "Consumable", 20, 20, 10m, 2m),
                CreateItem(202, "Máy phát điện", "Reusable", 5, 5, 5m, 5m)
            ]);

        var handler = new GetClosureTransferSuggestionsHandler(repository);

        var result = await handler.Handle(
            new GetClosureTransferSuggestionsQuery { DepotId = 10 },
            CancellationToken.None);

        Assert.Equal(1, result.SuggestedTargetDepotCount);
        Assert.DoesNotContain(result.SuggestedTransfers, x => x.TargetDepotId == null);
        Assert.All(
            result.SuggestedTransfers,
            transfer => Assert.Equal(2, transfer.TargetDepotId));

        var recommendedDepot = Assert.Single(result.TargetDepotMetrics, x => x.RecommendationRank == 1);
        Assert.Equal(2, recommendedDepot.DepotId);
        Assert.Equal(2, recommendedDepot.SuggestedItemLineCount);
        Assert.Equal(25, recommendedDepot.SuggestedUnitCount);
        Assert.Contains("gom 2 dòng hàng", recommendedDepot.RecommendationReason);
    }

    [Fact]
    public async Task Handle_SplitsReusableAcrossFewestPossibleDepots_WhenSingleDepotCannotFitAll()
    {
        var repository = new StubDepotRepository(
            sourceDepot: CreateDepot(10, "Kho nguồn", 10.0, 106.0, 1000m, 1000m),
            availableDepots:
            [
                CreateDepot(2, "Kho A", 10.05, 106.02, 20m, 20m),
                CreateDepot(3, "Kho B", 10.3, 106.4, 40m, 40m),
                CreateDepot(4, "Kho C", 11.0, 108.0, 10m, 10m)
            ],
            inventoryItems:
            [
                CreateItem(202, "Máy phát điện", "Reusable", 5, 5, 10m, 10m)
            ]);

        var handler = new GetClosureTransferSuggestionsHandler(repository);

        var result = await handler.Handle(
            new GetClosureTransferSuggestionsQuery { DepotId = 10 },
            CancellationToken.None);

        var plannedTransfers = result.SuggestedTransfers
            .Where(x => x.TargetDepotId.HasValue)
            .ToList();

        Assert.Equal(2, result.SuggestedTargetDepotCount);
        Assert.Equal(2, plannedTransfers.Count);
        Assert.All(plannedTransfers, x => Assert.Equal("Reusable", x.ItemType));
        Assert.Equal(5, plannedTransfers.Sum(x => x.SuggestedQuantity));
        Assert.Equal(0, result.UnallocatedItemLineCount);
        Assert.DoesNotContain(plannedTransfers, x => x.TargetDepotId == 4);
        Assert.Equal([3, 2], plannedTransfers.Select(x => x.TargetDepotId!.Value).ToArray());
        Assert.Equal(["SplitByCapacity", "SplitByCapacity"], plannedTransfers.Select(x => x.AllocationMode).ToArray());
    }

    private static DepotModel CreateDepot(
        int id,
        string name,
        double latitude,
        double longitude,
        decimal capacity,
        decimal weightCapacity)
    {
        return new DepotModel
        {
            Id = id,
            Name = name,
            Address = name,
            Location = new GeoLocation(latitude, longitude),
            Status = DepotStatus.Available,
            Capacity = capacity,
            WeightCapacity = weightCapacity,
            CurrentUtilization = 0m,
            CurrentWeightUtilization = 0m
        };
    }

    private static ClosureInventoryItemDto CreateItem(
        int itemModelId,
        string itemName,
        string itemType,
        int quantity,
        int transferableQuantity,
        decimal volumePerUnit,
        decimal weightPerUnit)
    {
        return new ClosureInventoryItemDto
        {
            ItemModelId = itemModelId,
            ItemName = itemName,
            CategoryName = "Danh mục",
            ItemType = itemType,
            Unit = "Đơn vị",
            Quantity = quantity,
            TransferableQuantity = transferableQuantity,
            BlockedQuantity = quantity - transferableQuantity,
            VolumePerUnit = volumePerUnit,
            WeightPerUnit = weightPerUnit
        };
    }

    private sealed class StubDepotRepository(
        DepotModel sourceDepot,
        IReadOnlyCollection<DepotModel> availableDepots,
        IReadOnlyCollection<ClosureInventoryItemDto> inventoryItems) : IDepotRepository
    {
        public Task CreateAsync(DepotModel depotModel, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateAsync(DepotModel depotModel, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task AssignManagerAsync(DepotModel depot, Guid newManagerId, Guid? assignedBy = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UnassignManagerAsync(DepotModel depot, Guid? unassignedBy = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UnassignSpecificManagersAsync(DepotModel depot, IReadOnlyList<Guid> userIds, Guid? unassignedBy = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<DepotModel>> GetAllPagedAsync(int pageNumber, int pageSize, IEnumerable<DepotStatus>? statuses = null, string? search = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IEnumerable<DepotModel>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(availableDepots.AsEnumerable());

        public Task<IEnumerable<DepotModel>> GetAvailableDepotsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(availableDepots.AsEnumerable());

        public Task<DepotModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == sourceDepot.Id ? sourceDepot : availableDepots.FirstOrDefault(x => x.Id == id));

        public Task<DepotModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> GetActiveDepotCountExcludingAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(int AsSourceCount, int AsRequesterCount)> GetNonTerminalSupplyRequestCountsAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<decimal> GetConsumableTransferVolumeAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(int AvailableCount, int InUseCount)> GetReusableItemCountsAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> GetConsumableInventoryRowCountAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DepotStatus?> GetStatusByIdAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(decimal PendingInboundVolume, decimal PendingInboundWeight)> GetPendingInboundLoadAsync(
            int depotId,
            CancellationToken cancellationToken = default)
            => Task.FromResult((0m, 0m));

        public Task<List<ClosureInventoryItemDto>> GetDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default)
            => Task.FromResult(inventoryItems.ToList());

        public Task<List<ClosureInventoryLotItemDto>> GetLotDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<ManagedDepotDto>> GetManagedDepotsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<DepotManagerInfoDto>> GetDepotManagersAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
