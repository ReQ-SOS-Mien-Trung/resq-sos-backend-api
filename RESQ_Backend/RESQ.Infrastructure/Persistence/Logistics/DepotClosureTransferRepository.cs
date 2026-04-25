using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class DepotClosureTransferRepository(IUnitOfWork unitOfWork, ResQDbContext dbContext)
    : IDepotClosureTransferRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ResQDbContext _dbContext = dbContext;

    public async Task<int> CreateAsync(DepotClosureTransferRecord record, CancellationToken cancellationToken = default)
    {
        var entity = ToEntity(record);
        await _unitOfWork.GetRepository<DepotClosureTransfer>().AddAsync(entity);
        await _unitOfWork.SaveAsync();
        return entity.Id;
    }

    public async Task<int> CreateAsync(
        DepotClosureTransferRecord record,
        IReadOnlyCollection<DepotClosureTransferItemRecord> items,
        CancellationToken cancellationToken = default)
    {
        var entity = ToEntity(record);
        entity.Items = items.Select(ToEntity).ToList();

        await _unitOfWork.GetRepository<DepotClosureTransfer>().AddAsync(entity);
        await _unitOfWork.SaveAsync();

        foreach (var item in items)
        {
            item.AttachToTransfer(entity.Id);
        }
        return entity.Id;
    }

    public async Task<DepotClosureTransferRecord?> GetByIdAsync(int transferId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotClosureTransfer>()
            .GetByPropertyAsync(x => x.Id == transferId, tracked: false);
        return entity == null ? null : ToDomain(entity);
    }

    public async Task<DepotClosureTransferRecord?> GetByClosureIdAsync(int closureId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotClosureTransfer>()
            .GetByPropertyAsync(x => x.ClosureId == closureId, tracked: false);
        return entity == null ? null : ToDomain(entity);
    }

    public async Task<List<DepotClosureTransferRecord>> GetAllByClosureIdAsync(int closureId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.Set<DepotClosureTransfer>()
            .AsNoTracking()
            .Where(x => x.ClosureId == closureId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    public async Task<DepotClosureTransferRecord?> GetActiveByClosureIdAsync(int closureId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotClosureTransfer>()
            .GetByPropertyAsync(
                x => x.ClosureId == closureId &&
                     x.Status != "Received" &&
                     x.Status != "Cancelled",
                tracked: false);
        return entity == null ? null : ToDomain(entity);
    }

    public async Task<bool> HasOpenTransfersAsync(int closureId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Set<DepotClosureTransfer>()
            .AnyAsync(
                x => x.ClosureId == closureId &&
                     x.Status != "Received" &&
                     x.Status != "Cancelled",
                cancellationToken);
    }

    public async Task<DepotClosureTransferRecord?> GetActiveIncomingByTargetDepotIdAsync(int targetDepotId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotClosureTransfer>()
            .GetByPropertyAsync(
                x => x.TargetDepotId == targetDepotId &&
                     x.Status != "Received" &&
                     x.Status != "Cancelled",
                tracked: false);
        return entity == null ? null : ToDomain(entity);
    }

    public async Task<List<DepotClosureTransferListItem>> GetByRelatedDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
    {
        return await (
            from transfer in _dbContext.DepotClosureTransfers.AsNoTracking()
            join sourceDepot in _dbContext.Depots.AsNoTracking()
                on transfer.SourceDepotId equals sourceDepot.Id
            join targetDepot in _dbContext.Depots.AsNoTracking()
                on transfer.TargetDepotId equals targetDepot.Id
            where transfer.SourceDepotId == depotId || transfer.TargetDepotId == depotId
            orderby transfer.CreatedAt descending
            select new DepotClosureTransferListItem
            {
                TransferId = transfer.Id,
                ClosureId = transfer.ClosureId,
                SourceDepotId = transfer.SourceDepotId,
                SourceDepotName = sourceDepot.Name,
                TargetDepotId = transfer.TargetDepotId,
                TargetDepotName = targetDepot.Name,
                Status = transfer.Status,
                CreatedAt = transfer.CreatedAt,
                SnapshotConsumableUnits = transfer.SnapshotConsumableUnits,
                SnapshotReusableUnits = transfer.SnapshotReusableUnits,
                ShippedAt = transfer.ShippedAt,
                ReceivedAt = transfer.ReceivedAt,
                CancelledAt = transfer.CancelledAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DepotClosureTransferItemRecord>> GetItemsByTransferIdAsync(int transferId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.Set<DepotClosureTransferItem>()
            .AsNoTracking()
            .Where(x => x.TransferId == transferId)
            .OrderBy(x => x.ItemType)
            .ThenBy(x => x.ItemName)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    public async Task<List<DepotClosureTransferItemDetailListItem>> GetDetailedItemsByTransferIdAsync(int transferId, CancellationToken cancellationToken = default)
    {
        var consumableItems = await _unitOfWork.Set<DepotClosureTransferConsumableReservation>()
            .AsNoTracking()
            .Where(x => x.TransferId == transferId)
            .Select(x => new DepotClosureTransferItemDetailListItem
            {
                ItemModelId = x.ItemModelId,
                ItemName = x.SupplyInventory != null && x.SupplyInventory.ItemModel != null
                    ? x.SupplyInventory.ItemModel.Name ?? string.Empty
                    : string.Empty,
                ItemType = x.SupplyInventory != null && x.SupplyInventory.ItemModel != null
                    ? x.SupplyInventory.ItemModel.ItemType ?? "Consumable"
                    : "Consumable",
                Unit = x.SupplyInventory != null && x.SupplyInventory.ItemModel != null
                    ? x.SupplyInventory.ItemModel.Unit
                    : null,
                Quantity = x.ReservedQuantity,
                LotId = x.SupplyInventoryLotId,
                ReusableItemId = null,
                SerialNumber = null
            })
            .ToListAsync(cancellationToken);

        var reusableItems = await _unitOfWork.Set<DepotClosureTransferReusableItem>()
            .AsNoTracking()
            .Where(x => x.TransferId == transferId
                        && x.ReusableItem != null
                        && x.ReusableItem.ItemModelId.HasValue)
            .Select(x => new DepotClosureTransferItemDetailListItem
            {
                ItemModelId = x.ReusableItem!.ItemModelId!.Value,
                ItemName = x.ReusableItem.ItemModel != null
                    ? x.ReusableItem.ItemModel.Name ?? string.Empty
                    : string.Empty,
                ItemType = x.ReusableItem.ItemModel != null
                    ? x.ReusableItem.ItemModel.ItemType ?? "Reusable"
                    : "Reusable",
                Unit = x.ReusableItem.ItemModel != null
                    ? x.ReusableItem.ItemModel.Unit
                    : null,
                Quantity = 1,
                LotId = null,
                ReusableItemId = x.ReusableItemId,
                SerialNumber = x.ReusableItem.SerialNumber
            })
            .ToListAsync(cancellationToken);

        return consumableItems
            .Concat(reusableItems)
            .OrderBy(x => x.ItemType)
            .ThenBy(x => x.ItemName)
            .ThenBy(x => x.LotId)
            .ThenBy(x => x.ReusableItemId)
            .ToList();
    }

    public async Task UpdateAsync(DepotClosureTransferRecord record, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<DepotClosureTransfer>();
        var entity = await repo.GetByPropertyAsync(x => x.Id == record.Id, tracked: true);
        if (entity == null) return;

        entity.Status = record.Status;
        entity.ShippedAt = record.ShippedAt;
        entity.ShippedBy = record.ShippedBy;
        entity.ShipNote = record.ShipNote;
        entity.ReceivedAt = record.ReceivedAt;
        entity.ReceivedBy = record.ReceivedBy;
        entity.ReceiveNote = record.ReceiveNote;
        entity.CancelledAt = record.CancelledAt;
        entity.CancelledBy = record.CancelledBy;
        entity.CancellationReason = record.CancellationReason;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveAsync();
    }

    // -- Mappers --------------------------------------------------------------

    private static DepotClosureTransfer ToEntity(DepotClosureTransferRecord record)
    {
        return new DepotClosureTransfer
        {
            ClosureId = record.ClosureId,
            SourceDepotId = record.SourceDepotId,
            TargetDepotId = record.TargetDepotId,
            Status = record.Status,
            CreatedAt = record.CreatedAt,
            TransferDeadlineAt = record.CreatedAt,
            SnapshotConsumableUnits = record.SnapshotConsumableUnits,
            SnapshotReusableUnits = record.SnapshotReusableUnits
        };
    }

    private static DepotClosureTransferItem ToEntity(DepotClosureTransferItemRecord record)
    {
        return new DepotClosureTransferItem
        {
            TransferId = record.TransferId,
            ItemModelId = record.ItemModelId,
            ItemName = record.ItemName,
            ItemType = record.ItemType,
            Unit = record.Unit,
            Quantity = record.Quantity
        };
    }

    private static DepotClosureTransferRecord ToDomain(DepotClosureTransfer entity)
    {
        return DepotClosureTransferRecord.FromPersistence(
            id: entity.Id,
            closureId: entity.ClosureId,
            sourceDepotId: entity.SourceDepotId,
            targetDepotId: entity.TargetDepotId,
            status: entity.Status,
            createdAt: entity.CreatedAt,
            shippedAt: entity.ShippedAt,
            shippedBy: entity.ShippedBy,
            shipNote: entity.ShipNote,
            receivedAt: entity.ReceivedAt,
            receivedBy: entity.ReceivedBy,
            receiveNote: entity.ReceiveNote,
            snapshotConsumableUnits: entity.SnapshotConsumableUnits,
            snapshotReusableUnits: entity.SnapshotReusableUnits,
            cancelledAt: entity.CancelledAt,
            cancelledBy: entity.CancelledBy,
            cancellationReason: entity.CancellationReason);
    }

    private static DepotClosureTransferItemRecord ToDomain(DepotClosureTransferItem entity)
    {
        return DepotClosureTransferItemRecord.FromPersistence(
            id: entity.Id,
            transferId: entity.TransferId,
            itemModelId: entity.ItemModelId,
            itemName: entity.ItemName,
            itemType: entity.ItemType,
            unit: entity.Unit,
            quantity: entity.Quantity);
    }
}


