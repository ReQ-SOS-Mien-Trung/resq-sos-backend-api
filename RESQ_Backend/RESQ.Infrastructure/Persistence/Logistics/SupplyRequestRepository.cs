using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using ItemModelEntity = RESQ.Infrastructure.Entities.Logistics.ItemModel;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class SupplyRequestRepository(IUnitOfWork unitOfWork) : ISupplyRequestRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<int> CreateAsync(
        int requestingDepotId,
        int sourceDepotId,
        List<(int ItemModelId, int Quantity)> items,
        string? note,
        Guid requestedBy,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var request = new DepotSupplyRequest
        {
            RequestingDepotId = requestingDepotId,
            SourceDepotId     = sourceDepotId,
            Note              = note,
            SourceStatus      = "Pending",
            RequestingStatus  = "WaitingForApproval",
            RequestedBy       = requestedBy,
            CreatedAt         = now
        };

        await _unitOfWork.GetRepository<DepotSupplyRequest>().AddAsync(request);
        await _unitOfWork.SaveAsync();

        var requestItems = items.Select(i => new DepotSupplyRequestItem
        {
            DepotSupplyRequestId = request.Id,
            ItemModelId          = i.ItemModelId,
            Quantity             = i.Quantity
        }).ToList();

        await _unitOfWork.GetRepository<DepotSupplyRequestItem>().AddRangeAsync(requestItems);
        await _unitOfWork.SaveAsync();

        return request.Id;
    }

    public async Task<SupplyRequestDetail?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotSupplyRequest>()
            .GetByPropertyAsync(r => r.Id == id, tracked: false, includeProperties: "Items");

        if (entity == null) return null;

        return new SupplyRequestDetail
        {
            Id                = entity.Id,
            RequestingDepotId = entity.RequestingDepotId,
            SourceDepotId     = entity.SourceDepotId,
            SourceStatus      = entity.SourceStatus,
            RequestingStatus  = entity.RequestingStatus,
            RequestedBy       = entity.RequestedBy,
            Items             = entity.Items.Select(i => (i.ItemModelId, i.Quantity)).ToList()
        };
    }

    public async Task UpdateStatusAsync(int id, string sourceStatus, string requestingStatus, string? rejectedReason, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotSupplyRequest>()
            .GetByPropertyAsync(r => r.Id == id, tracked: true)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{id}.");

        var now = DateTime.UtcNow;
        entity.SourceStatus     = sourceStatus;
        entity.RequestingStatus = requestingStatus;
        entity.UpdatedAt        = now;

        switch (sourceStatus)
        {
            case "Accepted":
                entity.RespondedAt = now;
                break;
            case "Rejected":
                entity.RespondedAt    = now;
                entity.RejectedReason = rejectedReason;
                break;
            case "Shipping":
                entity.ShippedAt = now;
                break;
            case "Completed":
                entity.CompletedAt ??= now;
                break;
        }

        await _unitOfWork.SaveAsync();
    }

    public async Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var manager = await _unitOfWork.GetRepository<DepotManager>()
            .GetByPropertyAsync(dm => dm.DepotId == depotId && dm.UnassignedAt == null, tracked: false);

        return manager?.UserId;
    }

    public async Task ReserveItemsAsync(
        int sourceDepotId,
        List<(int ItemModelId, int Quantity)> items,
        int supplyRequestId,
        Guid performedBy,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var (itemModelId, quantity) in items)
        {
            // Determine item type to choose the correct tracking path
            var itemModel = await _unitOfWork.GetRepository<ItemModelEntity>()
                .GetByPropertyAsync(x => x.Id == itemModelId, tracked: false)
                ?? throw new NotFoundException($"Vật tư #{itemModelId} không tồn tại trong hệ thống.");

            if (itemModel.ItemType == "Reusable")
            {
                // ── Reusable: per-unit asset tracking ──
                var availableUnits = await _unitOfWork.GetRepository<ReusableItem>()
                    .GetAllByPropertyAsync(x =>
                        x.DepotId == sourceDepotId &&
                        x.ItemModelId == itemModelId &&
                        x.Status == "Available");

                if (availableUnits.Count < quantity)
                    throw new BadRequestException(
                        $"Vật tư '{itemModel.Name}' (#{itemModelId}): kho nguồn chỉ có {availableUnits.Count} đơn vị khả dụng, yêu cầu {quantity}.");

                // Reserve exactly 'quantity' units
                var unitsToReserve = availableUnits.Take(quantity).ToList();
                foreach (var unit in unitsToReserve)
                {
                    unit.Status           = "Reserved";
                    unit.SupplyRequestId  = supplyRequestId;
                    unit.UpdatedAt        = now;

                    await _unitOfWork.GetRepository<ReusableItem>().UpdateAsync(unit);

                    await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                    {
                        ReusableItemId = unit.Id,
                        ActionType     = "Reserve",
                        QuantityChange = 1,
                        SourceType     = "SupplyRequest",
                        SourceId       = supplyRequestId,
                        PerformedBy    = performedBy,
                        Note           = $"Đặt trữ {itemModel.Name} (S/N: {unit.SerialNumber}) cho yêu cầu #{supplyRequestId}",
                        CreatedAt      = now
                    });
                }
            }
            else
            {
                // ── Consumable: quantity-based supply_inventory tracking ──
                var inventory = await _unitOfWork.GetRepository<SupplyInventory>()
                    .GetByPropertyAsync(x => x.DepotId == sourceDepotId && x.ItemModelId == itemModelId, tracked: true)
                    ?? throw new BadRequestException(
                        $"Kho nguồn không có vật tư '{itemModel.Name}' (#{itemModelId}) trong tồn kho.");

                var available = (inventory.Quantity ?? 0) - (inventory.ReservedQuantity ?? 0);
                if (available < quantity)
                    throw new BadRequestException(
                        $"Vật tư '{itemModel.Name}' (#{itemModelId}): tồn kho khả dụng ({available}) không đủ so với yêu cầu ({quantity}).");

                inventory.ReservedQuantity = (inventory.ReservedQuantity ?? 0) + quantity;
                inventory.LastStockedAt    = now;

                await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                {
                    DepotSupplyInventoryId = inventory.Id,
                    ActionType             = "Reserve",
                    QuantityChange         = quantity,
                    SourceType             = "SupplyRequest",
                    SourceId               = supplyRequestId,
                    PerformedBy            = performedBy,
                    Note                   = $"Đặt trữ {itemModel.Name} (#{itemModelId}) cho yêu cầu #{supplyRequestId}",
                    CreatedAt              = now
                });
            }
        }

        await _unitOfWork.SaveAsync();
    }

    public async Task TransferOutAsync(
        int sourceDepotId,
        List<(int ItemModelId, int Quantity)> items,
        int supplyRequestId,
        Guid performedBy,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var (itemModelId, quantity) in items)
        {
            var itemModel = await _unitOfWork.GetRepository<ItemModelEntity>()
                .GetByPropertyAsync(x => x.Id == itemModelId, tracked: false)
                ?? throw new NotFoundException($"Vật tư #{itemModelId} không tồn tại trong hệ thống.");

            if (itemModel.ItemType == "Reusable")
            {
                // ── Reusable: transition Reserved → InTransit per unit ──
                var reservedUnits = await _unitOfWork.GetRepository<ReusableItem>()
                    .GetAllByPropertyAsync(x =>
                        x.SupplyRequestId == supplyRequestId &&
                        x.ItemModelId     == itemModelId &&
                        x.Status          == "Reserved");

                if (reservedUnits.Count != quantity)
                    throw new BadRequestException(
                        $"Vật tư '{itemModel.Name}' (#{itemModelId}): tìm thấy {reservedUnits.Count} đơn vị đặt trữ, " +
                        $"không khớp với yêu cầu {quantity}. Quy trình có thể bị bỏ qua bước Accept.");

                foreach (var unit in reservedUnits)
                {
                    unit.Status    = "InTransit";
                    unit.DepotId   = null;   // en route — not at any depot
                    unit.UpdatedAt = now;

                    await _unitOfWork.GetRepository<ReusableItem>().UpdateAsync(unit);

                    await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                    {
                        ReusableItemId = unit.Id,
                        ActionType     = "TransferOut",
                        QuantityChange = 1,
                        SourceType     = "SupplyRequest",
                        SourceId       = supplyRequestId,
                        PerformedBy    = performedBy,
                        Note           = $"Xuất tiếp tế {itemModel.Name} (S/N: {unit.SerialNumber}) cho yêu cầu #{supplyRequestId}",
                        CreatedAt      = now
                    });
                }
            }
            else
            {
                // ── Consumable: deduct Quantity + ReservedQuantity from supply_inventory ──
                var inventory = await _unitOfWork.GetRepository<SupplyInventory>()
                    .GetByPropertyAsync(x => x.DepotId == sourceDepotId && x.ItemModelId == itemModelId, tracked: true)
                    ?? throw new BadRequestException(
                        $"Vật tư '{itemModel.Name}' (#{itemModelId}): không tìm thấy tồn kho tại kho nguồn. " +
                        "Quy trình có thể bị bỏ qua bước Accept.");

                var reserved = inventory.ReservedQuantity ?? 0;
                if (reserved < quantity)
                    throw new BadRequestException(
                        $"Vật tư '{itemModel.Name}' (#{itemModelId}): số lượng đặt trữ ({reserved}) không đủ so với yêu cầu ({quantity}). " +
                        "Quy trình có thể bị bỏ qua bước Accept.");

                if ((inventory.Quantity ?? 0) < quantity)
                    throw new BadRequestException(
                        $"Vật tư '{itemModel.Name}' (#{itemModelId}): tồn kho ({inventory.Quantity ?? 0}) không đủ so với yêu cầu ({quantity}).");

                inventory.Quantity         = (inventory.Quantity ?? 0) - quantity;
                inventory.ReservedQuantity = reserved - quantity;
                inventory.LastStockedAt    = now;

                await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                {
                    DepotSupplyInventoryId = inventory.Id,
                    ActionType             = "TransferOut",
                    QuantityChange         = quantity,
                    SourceType             = "SupplyRequest",
                    SourceId               = supplyRequestId,
                    PerformedBy            = performedBy,
                    Note                   = $"Xuất tiếp tế {itemModel.Name} (#{itemModelId}) cho yêu cầu #{supplyRequestId}",
                    CreatedAt              = now
                });
            }
        }

        await _unitOfWork.SaveAsync();
    }

    public async Task TransferInAsync(
        int requestingDepotId,
        List<(int ItemModelId, int Quantity)> items,
        int supplyRequestId,
        Guid performedBy,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var (itemModelId, quantity) in items)
        {
            var itemModel = await _unitOfWork.GetRepository<ItemModelEntity>()
                .GetByPropertyAsync(x => x.Id == itemModelId, tracked: false)
                ?? throw new NotFoundException($"Vật tư #{itemModelId} không tồn tại trong hệ thống.");

            if (itemModel.ItemType == "Reusable")
            {
                // ── Reusable: transition InTransit → Available at destination depot ──
                var inTransitUnits = await _unitOfWork.GetRepository<ReusableItem>()
                    .GetAllByPropertyAsync(x =>
                        x.SupplyRequestId == supplyRequestId &&
                        x.ItemModelId     == itemModelId &&
                        x.Status          == "InTransit");

                if (inTransitUnits.Count != quantity)
                    throw new BadRequestException(
                        $"Vật tư '{itemModel.Name}' (#{itemModelId}): tìm thấy {inTransitUnits.Count} đơn vị đang vận chuyển, " +
                        $"không khớp với yêu cầu {quantity}. Quy trình có thể bị bỏ qua bước Ship.");

                foreach (var unit in inTransitUnits)
                {
                    unit.DepotId         = requestingDepotId;
                    unit.Status          = "Available";
                    unit.SupplyRequestId = null;   // no longer tied to a transfer
                    unit.UpdatedAt       = now;

                    await _unitOfWork.GetRepository<ReusableItem>().UpdateAsync(unit);

                    await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                    {
                        ReusableItemId = unit.Id,
                        ActionType     = "TransferIn",
                        QuantityChange = 1,
                        SourceType     = "SupplyRequest",
                        SourceId       = supplyRequestId,
                        PerformedBy    = performedBy,
                        Note           = $"Nhận tiếp tế {itemModel.Name} (S/N: {unit.SerialNumber}) tại kho #{requestingDepotId} từ yêu cầu #{supplyRequestId}",
                        CreatedAt      = now
                    });
                }
            }
            else
            {
                // ── Consumable: increase Quantity at destination supply_inventory ──
                var inventory = await _unitOfWork.GetRepository<SupplyInventory>()
                    .GetByPropertyAsync(x => x.DepotId == requestingDepotId && x.ItemModelId == itemModelId, tracked: true);

                if (inventory == null)
                {
                    inventory = new SupplyInventory
                    {
                        DepotId          = requestingDepotId,
                        ItemModelId      = itemModelId,
                        Quantity         = 0,
                        ReservedQuantity = 0,
                        LastStockedAt    = now
                    };
                    await _unitOfWork.GetRepository<SupplyInventory>().AddAsync(inventory);
                    await _unitOfWork.SaveAsync(); // flush to get inventory.Id
                }

                inventory.Quantity      = (inventory.Quantity ?? 0) + quantity;
                inventory.LastStockedAt = now;

                await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                {
                    DepotSupplyInventoryId = inventory.Id,
                    ActionType             = "TransferIn",
                    QuantityChange         = quantity,
                    SourceType             = "SupplyRequest",
                    SourceId               = supplyRequestId,
                    PerformedBy            = performedBy,
                    Note                   = $"Nhận tiếp tế {itemModel.Name} (#{itemModelId}) từ yêu cầu #{supplyRequestId}",
                    CreatedAt              = now
                });
            }
        }

        await _unitOfWork.SaveAsync();
    }

    public async Task<PagedResult<SupplyRequestListItem>> GetPagedByDepotsAsync(
        List<int> depotIds,
        string? sourceStatus,
        string? requestingStatus,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var paged = await _unitOfWork.GetRepository<DepotSupplyRequest>()
            .GetPagedAsync(
                pageNumber,
                pageSize,
                filter: r => (depotIds.Contains(r.RequestingDepotId) || depotIds.Contains(r.SourceDepotId))
                             && (sourceStatus      == null || r.SourceStatus      == sourceStatus)
                             && (requestingStatus  == null || r.RequestingStatus  == requestingStatus),
                orderBy: q => q.OrderByDescending(r => r.CreatedAt),
                includeProperties: "RequestingDepot,SourceDepot,Items.ItemModel");

        var items = paged.Items.Select(entity => new SupplyRequestListItem
        {
            Id                  = entity.Id,
            RequestingDepotId   = entity.RequestingDepotId,
            RequestingDepotName = entity.RequestingDepot?.Name,
            SourceDepotId       = entity.SourceDepotId,
            SourceDepotName     = entity.SourceDepot?.Name,
            SourceStatus        = entity.SourceStatus,
            RequestingStatus    = entity.RequestingStatus,
            Note                = entity.Note,
            RejectedReason      = entity.RejectedReason,
            RequestedBy         = entity.RequestedBy,
            CreatedAt           = entity.CreatedAt,
            RespondedAt         = entity.RespondedAt,
            ShippedAt           = entity.ShippedAt,
            CompletedAt         = entity.CompletedAt,
            Items               = entity.Items.Select(i => new SupplyRequestItemDetail
            {
                ItemModelId   = i.ItemModelId,
                ItemModelName = i.ItemModel?.Name,
                Unit           = i.ItemModel?.Unit,
                Quantity       = i.Quantity
            }).ToList()
        }).ToList();

        return new PagedResult<SupplyRequestListItem>(items, paged.TotalCount, pageNumber, pageSize);
    }
}
