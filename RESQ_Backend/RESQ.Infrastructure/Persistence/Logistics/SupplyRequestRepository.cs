using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.Notifications;
using ItemModelEntity = RESQ.Infrastructure.Entities.Logistics.ItemModel;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class SupplyRequestRepository(IUnitOfWork unitOfWork) : ISupplyRequestRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<int> CreateAsync(
        int requestingDepotId,
        int sourceDepotId,
        List<(int ItemModelId, int Quantity)> items,
        SupplyRequestPriorityLevel priorityLevel,
        DateTime autoRejectAt,
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
            PriorityLevel     = priorityLevel.ToString(),
            SourceStatus      = "Pending",
            RequestingStatus  = "WaitingForApproval",
            RequestedBy       = requestedBy,
            CreatedAt         = now,
            AutoRejectAt      = autoRejectAt
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
            PriorityLevel     = ParsePriorityLevel(entity.PriorityLevel),
            SourceStatus      = entity.SourceStatus,
            RequestingStatus  = entity.RequestingStatus,
            RequestedBy       = entity.RequestedBy,
            CreatedAt         = entity.CreatedAt,
            AutoRejectAt      = entity.AutoRejectAt,
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

    public async Task<List<PendingSupplyRequestMonitorItem>> GetPendingForMonitoringAsync(CancellationToken cancellationToken = default)
    {
        var items = await _unitOfWork.GetRepository<DepotSupplyRequest>()
            .AsQueryable()
            .Where(x => x.SourceStatus == nameof(SourceDepotStatus.Pending)
                     && x.RequestingStatus == nameof(RequestingDepotStatus.WaitingForApproval))
            .OrderBy(x => x.AutoRejectAt ?? x.CreatedAt)
            .Select(x => new
            {
                Id = x.Id,
                SourceDepotId = x.SourceDepotId,
                RequestedBy = x.RequestedBy,
                PriorityLevel = x.PriorityLevel,
                CreatedAt = x.CreatedAt,
                AutoRejectAt = x.AutoRejectAt,
                HighEscalationNotified = x.HighEscalationNotified,
                UrgentEscalationNotified = x.UrgentEscalationNotified
            })
            .ToListAsync(cancellationToken);

        return items.Select(x => new PendingSupplyRequestMonitorItem
        {
            Id = x.Id,
            SourceDepotId = x.SourceDepotId,
            RequestedBy = x.RequestedBy,
            PriorityLevel = ParsePriorityLevel(x.PriorityLevel),
            CreatedAt = x.CreatedAt,
            AutoRejectAt = x.AutoRejectAt,
            HighEscalationNotified = x.HighEscalationNotified,
            UrgentEscalationNotified = x.UrgentEscalationNotified
        }).ToList();
    }

    public async Task SetAutoRejectAtAsync(int id, DateTime autoRejectAt, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotSupplyRequest>()
            .GetByPropertyAsync(x => x.Id == id, tracked: true)
            ?? throw new NotFoundException($"KhÃ´ng tÃ¬m tháº¥y yÃªu cáº§u cung cáº¥p #{id}.");

        if (entity.AutoRejectAt.HasValue)
            return;

        entity.AutoRejectAt = autoRejectAt;
        entity.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveAsync();
    }

    public async Task MarkHighEscalationNotifiedAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotSupplyRequest>()
            .GetByPropertyAsync(x => x.Id == id, tracked: true)
            ?? throw new NotFoundException($"KhÃ´ng tÃ¬m tháº¥y yÃªu cáº§u cung cáº¥p #{id}.");

        if (entity.HighEscalationNotified)
            return;

        var now = DateTime.UtcNow;
        entity.HighEscalationNotified = true;
        entity.HighEscalationNotifiedAt = now;
        entity.UpdatedAt = now;
        await _unitOfWork.SaveAsync();
    }

    public async Task MarkUrgentEscalationNotifiedAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotSupplyRequest>()
            .GetByPropertyAsync(x => x.Id == id, tracked: true)
            ?? throw new NotFoundException($"KhÃ´ng tÃ¬m tháº¥y yÃªu cáº§u cung cáº¥p #{id}.");

        if (entity.UrgentEscalationNotified)
            return;

        var now = DateTime.UtcNow;
        entity.UrgentEscalationNotified = true;
        entity.UrgentEscalationNotifiedAt = now;
        entity.UpdatedAt = now;
        await _unitOfWork.SaveAsync();
    }

    public async Task<bool> AutoRejectIfPendingAsync(int id, string rejectedReason, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotSupplyRequest>()
            .GetByPropertyAsync(x => x.Id == id, tracked: true);

        if (entity == null)
            return false;

        if (entity.SourceStatus != nameof(SourceDepotStatus.Pending)
            || entity.RequestingStatus != nameof(RequestingDepotStatus.WaitingForApproval))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        entity.SourceStatus = nameof(SourceDepotStatus.Rejected);
        entity.RequestingStatus = nameof(RequestingDepotStatus.Rejected);
        entity.RejectedReason = rejectedReason;
        entity.RespondedAt = now;
        entity.UpdatedAt = now;

        await _unitOfWork.SaveAsync();
        return true;
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
                        x.Status == nameof(ReusableItemStatus.Available));

                if (availableUnits.Count < quantity)
                    throw new BadRequestException(
                        $"Vật tư '{itemModel.Name}' (#{itemModelId}): kho nguồn chỉ có {availableUnits.Count} đơn vị khả dụng, yêu cầu {quantity}.");

                // Reserve exactly 'quantity' units
                var unitsToReserve = availableUnits.Take(quantity).ToList();
                foreach (var unit in unitsToReserve)
                {
                    unit.Status           = nameof(ReusableItemStatus.Reserved);
                    unit.SupplyRequestId  = supplyRequestId;
                    unit.UpdatedAt        = now;

                    await _unitOfWork.GetRepository<ReusableItem>().UpdateAsync(unit);

                    await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                    {
                        ReusableItemId = unit.Id,
                        ActionType     = "Reserve",
                        QuantityChange = 1,
                        SourceType     = InventorySourceType.Transfer.ToString(),
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

                var available = (inventory.Quantity ?? 0) - (inventory.MissionReservedQuantity + inventory.TransferReservedQuantity);
                if (available < quantity)
                    throw new BadRequestException(
                        $"Vật tư '{itemModel.Name}' (#{itemModelId}): tồn kho khả dụng ({available}) không đủ so với yêu cầu ({quantity}).");

                inventory.TransferReservedQuantity += quantity;
                inventory.LastStockedAt             = now;

                await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                {
                    DepotSupplyInventoryId = inventory.Id,
                    ActionType             = "Reserve",
                    QuantityChange         = quantity,
                    SourceType             = InventorySourceType.Transfer.ToString(),
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
        var hasReusableChanges = false;

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
                        x.Status          == nameof(ReusableItemStatus.Reserved));

                if (reservedUnits.Count != quantity)
                    throw new BadRequestException(
                        $"Vật tư '{itemModel.Name}' (#{itemModelId}): tìm thấy {reservedUnits.Count} đơn vị đặt trữ, " +
                        $"không khớp với yêu cầu {quantity}. Quy trình có thể bị bỏ qua bước Accept.");

                foreach (var unit in reservedUnits)
                {
                    unit.Status    = nameof(ReusableItemStatus.InTransit);
                    unit.DepotId   = null;   // en route — not at any depot
                    unit.UpdatedAt = now;

                    await _unitOfWork.GetRepository<ReusableItem>().UpdateAsync(unit);

                    await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                    {
                        ReusableItemId = unit.Id,
                        ActionType     = InventoryActionType.TransferOut.ToString(),
                        QuantityChange = 1,
                        SourceType     = InventorySourceType.Transfer.ToString(),
                        SourceId       = supplyRequestId,
                        PerformedBy    = performedBy,
                        Note           = $"Xuất tiếp tế {itemModel.Name} (S/N: {unit.SerialNumber}) cho yêu cầu #{supplyRequestId}",
                        CreatedAt      = now
                    });
                }

                hasReusableChanges = true;
            }
            else
            {
                // ── Consumable: deduct Quantity + ReservedQuantity from supply_inventory ──
                var inventory = await _unitOfWork.GetRepository<SupplyInventory>()
                    .GetByPropertyAsync(x => x.DepotId == sourceDepotId && x.ItemModelId == itemModelId, tracked: true)
                    ?? throw new BadRequestException(
                        $"Vật tư '{itemModel.Name}' (#{itemModelId}): không tìm thấy tồn kho tại kho nguồn. " +
                        "Quy trình có thể bị bỏ qua bước Accept.");

                var reserved = inventory.TransferReservedQuantity;
                if (reserved < quantity)
                    throw new BadRequestException(
                        $"Vật tư '{itemModel.Name}' (#{itemModelId}): số lượng đặt trữ tiếp tế ({reserved}) không đủ so với yêu cầu ({quantity}). " +
                        "Quy trình có thể bị bỏ qua bước Accept.");

                if ((inventory.Quantity ?? 0) < quantity)
                    throw new BadRequestException(
                        $"Vật tư '{itemModel.Name}' (#{itemModelId}): tồn kho ({inventory.Quantity ?? 0}) không đủ so với yêu cầu ({quantity}).");

                inventory.Quantity                  = (inventory.Quantity ?? 0) - quantity;
                inventory.TransferReservedQuantity  = reserved - quantity;
                inventory.LastStockedAt             = now;

                // ── FEFO lot deduction ──────────────────────────────────────────
                var lots = await _unitOfWork.SetTracked<SupplyInventoryLot>()
                    .Where(l => l.SupplyInventoryId == inventory.Id && l.RemainingQuantity > 0)
                    .OrderBy(l => l.ExpiredDate == null ? 1 : 0)  // items WITH expiry first
                    .ThenBy(l => l.ExpiredDate)                    // soonest expiry first (FEFO)
                    .ThenBy(l => l.ReceivedDate)                   // oldest received first (FIFO tie-breaker)
                    .ToListAsync(cancellationToken);

                if (lots.Count > 0)
                {
                    var remaining = quantity;
                    foreach (var lot in lots)
                    {
                        if (remaining <= 0) break;
                        var deduct = Math.Min(lot.RemainingQuantity, remaining);
                        lot.RemainingQuantity -= deduct;
                        remaining -= deduct;

                        await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                        {
                            DepotSupplyInventoryId = inventory.Id,
                            SupplyInventoryLotId   = lot.Id,
                            ActionType             = InventoryActionType.TransferOut.ToString(),
                            QuantityChange         = deduct,
                            SourceType             = InventorySourceType.Transfer.ToString(),
                            SourceId               = supplyRequestId,
                            PerformedBy            = performedBy,
                            Note                   = $"Xuất tiếp tế FEFO lô #{lot.Id} {itemModel.Name} (#{itemModelId}) SL {deduct} cho yêu cầu #{supplyRequestId}",
                            CreatedAt              = now
                        });
                    }

                    if (remaining > 0)
                        throw new InvalidOperationException(
                            $"Vật tư '{itemModel.Name}' (#{itemModelId}): không đủ lô để xuất tiếp tế {quantity} đơn vị.");
                }
                else
                {
                    // Fallback: no lots yet (legacy data) — single log
                    await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                    {
                        DepotSupplyInventoryId = inventory.Id,
                        ActionType             = InventoryActionType.TransferOut.ToString(),
                        QuantityChange         = quantity,
                        SourceType             = InventorySourceType.Transfer.ToString(),
                        SourceId               = supplyRequestId,
                        PerformedBy            = performedBy,
                        Note                   = $"Xuất tiếp tế {itemModel.Name} (#{itemModelId}) SL {quantity} cho yêu cầu #{supplyRequestId} (legacy – không có lô)",
                        CreatedAt              = now
                    });
                }
            }
        }

        // Reusable items have DepotId set to null BEFORE UpdateAsync, so
        // CaptureDepotRealtimeOutboxEntries cannot recover the source depot from the
        // change tracker. Emit the outbox event explicitly here so the source depot's
        // realtime inventory view is refreshed when items go InTransit.
        if (hasReusableChanges)
        {
            await _unitOfWork.GetRepository<DepotRealtimeOutbox>().AddAsync(new DepotRealtimeOutbox
            {
                Id            = Guid.NewGuid(),
                DepotId       = sourceDepotId,
                EventType     = "DepotUpdated",
                Operation     = "Update",
                PayloadKind   = "Full",
                IsCritical    = false,
                ChangedFields = "ReusableItems",
                Status        = "Pending",
                AttemptCount  = 0,
                NextAttemptAt = now,
                OccurredAt    = now,
                CreatedAt     = now,
                UpdatedAt     = now
            });
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
                        x.Status          == nameof(ReusableItemStatus.InTransit));

                if (inTransitUnits.Count != quantity)
                    throw new BadRequestException(
                        $"Vật tư '{itemModel.Name}' (#{itemModelId}): tìm thấy {inTransitUnits.Count} đơn vị đang vận chuyển, " +
                        $"không khớp với yêu cầu {quantity}. Quy trình có thể bị bỏ qua bước Ship.");

                foreach (var unit in inTransitUnits)
                {
                    unit.DepotId         = requestingDepotId;
                    unit.Status          = nameof(ReusableItemStatus.Available);
                    unit.SupplyRequestId = null;   // no longer tied to a transfer
                    unit.UpdatedAt       = now;

                    await _unitOfWork.GetRepository<ReusableItem>().UpdateAsync(unit);

                    await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                    {
                        ReusableItemId = unit.Id,
                        ActionType     = InventoryActionType.TransferIn.ToString(),
                        QuantityChange = 1,
                        SourceType     = InventorySourceType.Transfer.ToString(),
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
                        DepotId                   = requestingDepotId,
                        ItemModelId               = itemModelId,
                        Quantity                  = 0,
                        MissionReservedQuantity   = 0,
                        TransferReservedQuantity  = 0,
                        LastStockedAt             = now
                    };
                    await _unitOfWork.GetRepository<SupplyInventory>().AddAsync(inventory);
                    await _unitOfWork.SaveAsync(); // flush to get inventory.Id
                }

                // Tạo lô mới cho hàng được tiếp tế vào kho
                var lot = new SupplyInventoryLot
                {
                    SupplyInventoryId = inventory.Id,
                    Quantity          = quantity,
                    RemainingQuantity = quantity,
                    ReceivedDate      = now,
                    ExpiredDate       = null,
                    SourceType        = InventorySourceType.Transfer.ToString(),
                    SourceId          = supplyRequestId,
                    CreatedAt         = now
                };
                await _unitOfWork.GetRepository<SupplyInventoryLot>().AddAsync(lot);
                await _unitOfWork.SaveAsync(); // flush để lấy lot.Id

                inventory.Quantity      = (inventory.Quantity ?? 0) + quantity;
                inventory.LastStockedAt = now;

                await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                {
                    DepotSupplyInventoryId = inventory.Id,
                    SupplyInventoryLotId   = lot.Id,
                    ActionType             = InventoryActionType.TransferIn.ToString(),
                    QuantityChange         = quantity,
                    SourceType             = InventorySourceType.Transfer.ToString(),
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
            PriorityLevel       = ParsePriorityLevel(entity.PriorityLevel),
            SourceStatus        = entity.SourceStatus,
            RequestingStatus    = entity.RequestingStatus,
            Note                = entity.Note,
            RejectedReason      = entity.RejectedReason,
            RequestedBy         = entity.RequestedBy,
            CreatedAt           = entity.CreatedAt,
            AutoRejectAt        = entity.AutoRejectAt,
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

    private static SupplyRequestPriorityLevel ParsePriorityLevel(string? priorityLevel)
        => Enum.TryParse<SupplyRequestPriorityLevel>(priorityLevel, true, out var parsed)
            ? parsed
            : SupplyRequestPriorityLevel.Medium;
}
