using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Logistics;
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

    public async Task UpdateStatusAsync(int id, string sourceStatus, string requestingStatus, string? rejectedReason, Guid? performedBy = null, CancellationToken cancellationToken = default)
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
                entity.AcceptedBy  = performedBy;
                break;
            case "Rejected":
                entity.RespondedAt    = now;
                entity.RejectedReason = rejectedReason;
                entity.RejectedBy     = performedBy;
                break;
            case "Preparing":
                entity.PreparedBy = performedBy;
                break;
            case "Shipping":
                entity.ShippedAt = now;
                entity.ShippedBy = performedBy;
                break;
            case "Completed" when requestingStatus == "Received":
                // ConfirmSupplyRequest: requesting depot xác nhận nhận hàng
                entity.CompletedAt  ??= now;
                entity.ConfirmedBy  = performedBy;
                break;
            case "Completed":
                // CompleteSupplyRequest: source depot xác nhận giao hàng xong
                entity.CompletedAt  ??= now;
                entity.CompletedBy  = performedBy;
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
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{id}.");

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
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{id}.");

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
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{id}.");

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
        var normalizedItems = items
            .GroupBy(x => x.ItemModelId)
            .Select(g => (ItemModelId: g.Key, Quantity: g.Sum(x => x.Quantity)))
            .ToList();

        var existingReusableReservations = await _unitOfWork.SetTracked<DepotSupplyRequestReusableItem>()
            .AnyAsync(x => x.SupplyRequestId == supplyRequestId && x.Status != "Released", cancellationToken);
        var existingConsumableReservations = await _unitOfWork.SetTracked<DepotSupplyRequestConsumableReservation>()
            .AnyAsync(x => x.SupplyRequestId == supplyRequestId && x.Status != "Released", cancellationToken);

        if (existingReusableReservations || existingConsumableReservations)
        {
            throw new ConflictException(
                $"Yêu cầu tiếp tế #{supplyRequestId} đã có reservation trước đó. Vui lòng tải lại dữ liệu trước khi thao tác tiếp.");
        }

        var itemModelIds = normalizedItems.Select(x => x.ItemModelId).ToList();
        var itemModels = await _unitOfWork.Set<ItemModelEntity>()
            .Where(x => itemModelIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var consumableItems = normalizedItems
            .Where(x => itemModels.TryGetValue(x.ItemModelId, out var model) && string.Equals(model.ItemType, "Consumable", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (consumableItems.Count > 0)
        {
            var consumableItemModelIds = consumableItems.Select(x => x.ItemModelId).ToList();
            var inventories = await _unitOfWork.SetTracked<SupplyInventory>()
                .Where(inv => inv.DepotId == sourceDepotId && consumableItemModelIds.Contains(inv.ItemModelId ?? 0))
                .Include(inv => inv.Lots)
                .ToListAsync(cancellationToken);

            var activeSupplyReservations = await _unitOfWork.Set<DepotSupplyRequestConsumableReservation>()
                .AsNoTracking()
                .Where(r => r.SupplyInventory.DepotId == sourceDepotId
                         && consumableItemModelIds.Contains(r.ItemModelId)
                         && (r.Status == "Reserved" || r.Status == "Shipped"))
                .Select(r => new
                {
                    r.SupplyInventoryId,
                    r.SupplyInventoryLotId,
                    r.ReservedQuantity
                })
                .ToListAsync(cancellationToken);

            var activeClosureReservations = await _unitOfWork.Set<DepotClosureTransferConsumableReservation>()
                .AsNoTracking()
                .Where(r => r.SupplyInventory.DepotId == sourceDepotId
                         && consumableItemModelIds.Contains(r.ItemModelId)
                         && (r.Status == "Reserved" || r.Status == "Shipped"))
                .Select(r => new
                {
                    r.SupplyInventoryId,
                    r.SupplyInventoryLotId,
                    r.ReservedQuantity
                })
                .ToListAsync(cancellationToken);

            var newReservations = new List<DepotSupplyRequestConsumableReservation>();

            foreach (var ci in consumableItems)
            {
                var itemModel = itemModels[ci.ItemModelId];
                var inventory = inventories.FirstOrDefault(x => x.ItemModelId == ci.ItemModelId)
                    ?? throw new BadRequestException(
                        $"Kho nguồn không có vật phẩm '{itemModel.Name}' (#{ci.ItemModelId}) trong tồn kho.");

                var available = (inventory.Quantity ?? 0) - (inventory.MissionReservedQuantity + inventory.TransferReservedQuantity);
                if (available < ci.Quantity)
                {
                    throw new BadRequestException(
                        $"Vật phẩm '{itemModel.Name}' (#{ci.ItemModelId}): tồn kho khả dụng ({available}) không đủ so với yêu cầu ({ci.Quantity}).");
                }

                inventory.TransferReservedQuantity += ci.Quantity;
                inventory.LastStockedAt = now;

                var remainingToReserve = ci.Quantity;
                var lots = inventory.Lots
                    .Where(l => l.RemainingQuantity > 0)
                    .OrderBy(l => l.ExpiredDate == null ? 1 : 0)
                    .ThenBy(l => l.ExpiredDate)
                    .ThenBy(l => l.ReceivedDate)
                    .ThenBy(l => l.Id)
                    .ToList();

                foreach (var lot in lots)
                {
                    if (remainingToReserve <= 0) break;

                    var alreadyReserved = activeSupplyReservations
                        .Where(x => x.SupplyInventoryLotId == lot.Id)
                        .Sum(x => x.ReservedQuantity)
                        + activeClosureReservations
                            .Where(x => x.SupplyInventoryLotId == lot.Id)
                            .Sum(x => x.ReservedQuantity);

                    var lotAvailable = Math.Max(0, lot.RemainingQuantity - alreadyReserved);
                    if (lotAvailable <= 0) continue;

                    var reservedQuantity = Math.Min(lotAvailable, remainingToReserve);
                    newReservations.Add(new DepotSupplyRequestConsumableReservation
                    {
                        SupplyRequestId = supplyRequestId,
                        SupplyInventoryId = inventory.Id,
                        SupplyInventoryLotId = lot.Id,
                        ItemModelId = ci.ItemModelId,
                        ReservedQuantity = reservedQuantity,
                        Status = "Reserved",
                        ReceivedDate = lot.ReceivedDate,
                        ExpiredDate = lot.ExpiredDate,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                    remainingToReserve -= reservedQuantity;
                }

                if (remainingToReserve > 0)
                {
                    var trackedByLotsQuantity = lots.Sum(l => l.RemainingQuantity);
                    var reservedLegacyQuantity = activeSupplyReservations
                        .Where(x => x.SupplyInventoryId == inventory.Id && x.SupplyInventoryLotId == null)
                        .Sum(x => x.ReservedQuantity)
                        + activeClosureReservations
                            .Where(x => x.SupplyInventoryId == inventory.Id && x.SupplyInventoryLotId == null)
                            .Sum(x => x.ReservedQuantity);

                    var availableLegacyQuantity = Math.Max(0, (inventory.Quantity ?? 0) - trackedByLotsQuantity - reservedLegacyQuantity);
                    if (availableLegacyQuantity < remainingToReserve)
                    {
                        throw new ConflictException(
                            $"Không đủ lô hoặc tồn legacy khả dụng để đặt trữ vật phẩm '{itemModel.Name}' (#{ci.ItemModelId}) cho yêu cầu #{supplyRequestId}. Còn thiếu {remainingToReserve} đơn vị.");
                    }

                    newReservations.Add(new DepotSupplyRequestConsumableReservation
                    {
                        SupplyRequestId = supplyRequestId,
                        SupplyInventoryId = inventory.Id,
                        SupplyInventoryLotId = null,
                        ItemModelId = ci.ItemModelId,
                        ReservedQuantity = remainingToReserve,
                        Status = "Reserved",
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }

                await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                {
                    DepotSupplyInventoryId = inventory.Id,
                    ActionType = InventoryActionType.Reserve.ToString(),
                    QuantityChange = ci.Quantity,
                    SourceType = InventorySourceType.Transfer.ToString(),
                    SourceId = supplyRequestId,
                    PerformedBy = performedBy,
                    Note = $"Đặt trữ {itemModel.Name} (#{ci.ItemModelId}) cho yêu cầu #{supplyRequestId}",
                    CreatedAt = now
                });
            }

            if (newReservations.Count > 0)
            {
                await _unitOfWork.GetRepository<DepotSupplyRequestConsumableReservation>().AddRangeAsync(newReservations);
            }
        }

        var reusableItems = normalizedItems
            .Where(x => itemModels.TryGetValue(x.ItemModelId, out var model) && string.Equals(model.ItemType, "Reusable", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (reusableItems.Count > 0)
        {
            var reusableItemModelIds = reusableItems.Select(x => x.ItemModelId).ToList();
            var availableUnits = await _unitOfWork.SetTracked<ReusableItem>()
                .Where(x => x.DepotId == sourceDepotId
                         && reusableItemModelIds.Contains(x.ItemModelId ?? 0)
                         && !x.IsDeleted
                         && x.Status == nameof(ReusableItemStatus.Available))
                .OrderBy(x => x.ItemModelId)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

            var newInventoryLogs = new List<InventoryLog>();

            foreach (var ri in reusableItems)
            {
                var itemModel = itemModels[ri.ItemModelId];
                var pickedUnits = availableUnits
                    .Where(x => x.ItemModelId == ri.ItemModelId)
                    .Take(ri.Quantity)
                    .ToList();

                if (pickedUnits.Count < ri.Quantity)
                {
                    throw new BadRequestException(
                        $"Vật phẩm '{itemModel.Name}' (#{ri.ItemModelId}): kho nguồn chỉ có {pickedUnits.Count} đơn vị khả dụng, yêu cầu {ri.Quantity}.");
                }

                foreach (var unit in pickedUnits)
                {
                    unit.Status = nameof(ReusableItemStatus.Reserved);
                    unit.UpdatedAt = now;

                    await _unitOfWork.GetRepository<DepotSupplyRequestReusableItem>().AddAsync(new DepotSupplyRequestReusableItem
                    {
                        SupplyRequestId = supplyRequestId,
                        ReusableItemId = unit.Id,
                        Status = "Reserved",
                        CreatedAt = now,
                        UpdatedAt = now
                    });

                    newInventoryLogs.Add(new InventoryLog
                    {
                        ReusableItemId = unit.Id,
                        ActionType = InventoryActionType.Reserve.ToString(),
                        QuantityChange = 1,
                        SourceType = InventorySourceType.Transfer.ToString(),
                        SourceId = supplyRequestId,
                        PerformedBy = performedBy,
                        Note = $"Đặt trữ {itemModel.Name} (S/N: {unit.SerialNumber}) cho yêu cầu #{supplyRequestId}",
                        CreatedAt = now
                    });
                }

                availableUnits.RemoveAll(x => x.ItemModelId == ri.ItemModelId && pickedUnits.Any(p => p.Id == x.Id));
            }

            if (newInventoryLogs.Count > 0)
            {
                await _unitOfWork.GetRepository<InventoryLog>().AddRangeAsync(newInventoryLogs);
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
        decimal totalVolume = 0m;
        decimal totalWeight = 0m;
        var normalizedItems = items
            .GroupBy(x => x.ItemModelId)
            .Select(g => (ItemModelId: g.Key, Quantity: g.Sum(x => x.Quantity)))
            .ToList();

        foreach (var (itemModelId, quantity) in normalizedItems)
        {
            var itemModel = await _unitOfWork.GetRepository<ItemModelEntity>()
                .GetByPropertyAsync(x => x.Id == itemModelId, tracked: false)
                ?? throw new NotFoundException($"vật phẩm #{itemModelId} không tồn tại trong hệ thống.");

            totalVolume += (itemModel.VolumePerUnit ?? 0m) * quantity;
            totalWeight += (itemModel.WeightPerUnit ?? 0m) * quantity;

            if (itemModel.ItemType == "Reusable")
            {
                var reservedUnits = await _unitOfWork.SetTracked<DepotSupplyRequestReusableItem>()
                    .Include(x => x.ReusableItem)
                    .Where(x => x.SupplyRequestId == supplyRequestId
                             && x.Status == "Reserved"
                             && x.ReusableItem != null
                             && x.ReusableItem.DepotId == sourceDepotId
                             && x.ReusableItem.ItemModelId == itemModelId
                             && x.ReusableItem.Status == nameof(ReusableItemStatus.Reserved))
                    .OrderBy(x => x.ReusableItemId)
                    .ToListAsync(cancellationToken);

                if (reservedUnits.Count != quantity)
                    throw new BadRequestException(
                        $"Vật phẩm '{itemModel.Name}' (#{itemModelId}): tìm thấy {reservedUnits.Count} đơn vị đặt trữ, " +
                        $"không khớp với yêu cầu {quantity}. Quy trình có thể bị bỏ qua bước Prepare.");

                foreach (var reservation in reservedUnits)
                {
                    var unit = reservation.ReusableItem!;
                    unit.Status    = nameof(ReusableItemStatus.InTransit);
                    unit.DepotId   = null;   // en route - not at any depot
                    unit.UpdatedAt = now;
                    reservation.Status = "Shipped";
                    reservation.UpdatedAt = now;

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
                var inventory = await _unitOfWork.SetTracked<SupplyInventory>()
                    .Include(x => x.Lots)
                    .FirstOrDefaultAsync(x => x.DepotId == sourceDepotId && x.ItemModelId == itemModelId, cancellationToken)
                    ?? throw new BadRequestException(
                        $"vật phẩm '{itemModel.Name}' (#{itemModelId}): không tìm thấy tồn kho tại kho nguồn. " +
                        "Quy trình có thể bị bỏ qua bước Prepare.");

                var reservations = await _unitOfWork.SetTracked<DepotSupplyRequestConsumableReservation>()
                    .Where(x => x.SupplyRequestId == supplyRequestId
                             && x.ItemModelId == itemModelId
                             && x.Status == "Reserved")
                    .OrderBy(x => x.Id)
                    .ToListAsync(cancellationToken);

                var reserved = reservations.Sum(x => x.ReservedQuantity);
                if (reserved != quantity)
                    throw new BadRequestException(
                        $"vật phẩm '{itemModel.Name}' (#{itemModelId}): số lượng đặt trữ tiếp tế ({reserved}) không đủ so với yêu cầu ({quantity}). " +
                        "Quy trình có thể bị bỏ qua bước Prepare.");

                if ((inventory.Quantity ?? 0) < quantity)
                    throw new BadRequestException(
                        $"vật phẩm '{itemModel.Name}' (#{itemModelId}): tồn kho ({inventory.Quantity ?? 0}) không đủ so với yêu cầu ({quantity}).");

                inventory.Quantity                  = (inventory.Quantity ?? 0) - quantity;
                inventory.TransferReservedQuantity  = reserved - quantity;
                inventory.LastStockedAt             = now;
                if ((inventory.Quantity ?? 0) <= 0
                    && inventory.MissionReservedQuantity == 0
                    && inventory.TransferReservedQuantity == 0)
                {
                    inventory.IsDeleted = true;
                }

                foreach (var reservation in reservations.Where(x => x.SupplyInventoryLotId.HasValue))
                {
                    var lot = inventory.Lots.FirstOrDefault(l => l.Id == reservation.SupplyInventoryLotId)
                        ?? throw new ConflictException(
                            $"Không tìm thấy lô #{reservation.SupplyInventoryLotId} đã được reserve cho yêu cầu #{supplyRequestId}.");

                    if (lot.RemainingQuantity < reservation.ReservedQuantity)
                    {
                        throw new ConflictException(
                            $"Lô #{lot.Id} không còn đủ số lượng để xuất cho yêu cầu #{supplyRequestId}. Cần {reservation.ReservedQuantity}, còn {lot.RemainingQuantity}.");
                    }

                    lot.RemainingQuantity -= reservation.ReservedQuantity;
                    reservation.Status = "Shipped";
                    reservation.UpdatedAt = now;

                    await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                    {
                        DepotSupplyInventoryId = inventory.Id,
                        SupplyInventoryLotId   = lot.Id,
                        ActionType             = InventoryActionType.TransferOut.ToString(),
                        QuantityChange         = reservation.ReservedQuantity,
                        SourceType             = InventorySourceType.Transfer.ToString(),
                        SourceId               = supplyRequestId,
                        PerformedBy            = performedBy,
                        Note                   = $"Xuất tiếp tế FEFO lô #{lot.Id} {itemModel.Name} (#{itemModelId}) SL {reservation.ReservedQuantity} cho yêu cầu #{supplyRequestId}",
                        CreatedAt              = now
                    });
                }

                foreach (var reservation in reservations.Where(x => !x.SupplyInventoryLotId.HasValue))
                {
                    reservation.Status = "Shipped";
                    reservation.UpdatedAt = now;

                    await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                    {
                        DepotSupplyInventoryId = inventory.Id,
                        ActionType             = InventoryActionType.TransferOut.ToString(),
                        QuantityChange         = reservation.ReservedQuantity,
                        SourceType             = InventorySourceType.Transfer.ToString(),
                        SourceId               = supplyRequestId,
                        PerformedBy            = performedBy,
                        Note                   = $"Xuất tiếp tế {itemModel.Name} (#{itemModelId}) SL {reservation.ReservedQuantity} cho yêu cầu #{supplyRequestId} (legacy - không có lô)",
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

        if (totalVolume > 0m || totalWeight > 0m)
        {
            var sourceDepot = await _unitOfWork.GetRepository<RESQ.Infrastructure.Entities.Logistics.Depot>()
                .GetByPropertyAsync(x => x.Id == sourceDepotId, tracked: true);
            
            if (sourceDepot != null)
            {
                sourceDepot.CurrentUtilization = Math.Max(0m, (sourceDepot.CurrentUtilization ?? 0m) - totalVolume);
                sourceDepot.CurrentWeightUtilization = Math.Max(0m, (sourceDepot.CurrentWeightUtilization ?? 0m) - totalWeight);
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
        decimal totalVolume = 0m;
        decimal totalWeight = 0m;
        var normalizedItems = items
            .GroupBy(x => x.ItemModelId)
            .Select(g => (ItemModelId: g.Key, Quantity: g.Sum(x => x.Quantity)))
            .ToList();

        foreach (var (itemModelId, quantity) in normalizedItems)
        {
            var itemModel = await _unitOfWork.GetRepository<ItemModelEntity>()
                .GetByPropertyAsync(x => x.Id == itemModelId, tracked: false)
                ?? throw new NotFoundException($"vật phẩm #{itemModelId} không tồn tại trong hệ thống.");

            totalVolume += (itemModel.VolumePerUnit ?? 0m) * quantity;
            totalWeight += (itemModel.WeightPerUnit ?? 0m) * quantity;

            if (itemModel.ItemType == "Reusable")
            {
                var shippedUnits = await _unitOfWork.SetTracked<DepotSupplyRequestReusableItem>()
                    .Include(x => x.ReusableItem)
                    .Where(x => x.SupplyRequestId == supplyRequestId
                             && x.Status == "Shipped"
                             && x.ReusableItem != null
                             && x.ReusableItem.ItemModelId == itemModelId
                             && x.ReusableItem.Status == nameof(ReusableItemStatus.InTransit))
                    .OrderBy(x => x.ReusableItemId)
                    .ToListAsync(cancellationToken);

                if (shippedUnits.Count != quantity)
                    throw new BadRequestException(
                        $"vật phẩm '{itemModel.Name}' (#{itemModelId}): tìm thấy {shippedUnits.Count} đơn vị đang vận chuyển, " +
                        $"không khớp với yêu cầu {quantity}. Quy trình có thể bị bỏ qua bước Ship.");

                foreach (var reservation in shippedUnits)
                {
                    var unit = reservation.ReusableItem!;
                    unit.DepotId         = requestingDepotId;
                    unit.Status          = nameof(ReusableItemStatus.Available);
                    unit.UpdatedAt       = now;
                    reservation.Status = "Received";
                    reservation.UpdatedAt = now;

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
                var reservations = await _unitOfWork.SetTracked<DepotSupplyRequestConsumableReservation>()
                    .Where(x => x.SupplyRequestId == supplyRequestId
                             && x.ItemModelId == itemModelId
                             && x.Status == "Shipped")
                    .OrderBy(x => x.Id)
                    .ToListAsync(cancellationToken);

                var shippedQuantity = reservations.Sum(x => x.ReservedQuantity);
                if (shippedQuantity != quantity)
                {
                    throw new ConflictException(
                        $"Reservation của vật phẩm '{itemModel.Name}' (#{itemModelId}) cho yêu cầu #{supplyRequestId} không khớp. Cần {quantity}, hệ thống đang ghi nhận {shippedQuantity} đơn vị đã xuất.");
                }

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
                    await _unitOfWork.SaveAsync();
                }

                foreach (var reservation in reservations)
                {
                    var lot = new SupplyInventoryLot
                    {
                        SupplyInventoryId = inventory.Id,
                        Quantity = reservation.ReservedQuantity,
                        RemainingQuantity = reservation.ReservedQuantity,
                        ReceivedDate = reservation.ReceivedDate ?? now,
                        ExpiredDate = reservation.ExpiredDate,
                        SourceType = InventorySourceType.Transfer.ToString(),
                        SourceId = supplyRequestId,
                        CreatedAt = now
                    };

                    await _unitOfWork.GetRepository<SupplyInventoryLot>().AddAsync(lot);
                    await _unitOfWork.SaveAsync();

                    inventory.Quantity = (inventory.Quantity ?? 0) + reservation.ReservedQuantity;
                    inventory.LastStockedAt = now;
                    reservation.Status = "Received";
                    reservation.UpdatedAt = now;

                    await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                    {
                        DepotSupplyInventoryId = inventory.Id,
                        SupplyInventoryLotId   = lot.Id,
                        ActionType             = InventoryActionType.TransferIn.ToString(),
                        QuantityChange         = reservation.ReservedQuantity,
                        SourceType             = InventorySourceType.Transfer.ToString(),
                        SourceId               = supplyRequestId,
                        PerformedBy            = performedBy,
                        Note                   = $"Nhận tiếp tế {itemModel.Name} (#{itemModelId}) từ yêu cầu #{supplyRequestId}",
                        CreatedAt              = now
                    });
                }
            }
        }

        if (totalVolume > 0m || totalWeight > 0m)
        {
            var requestingDepot = await _unitOfWork.GetRepository<RESQ.Infrastructure.Entities.Logistics.Depot>()
                .GetByPropertyAsync(x => x.Id == requestingDepotId, tracked: true);
            
            if (requestingDepot != null)
            {
                requestingDepot.CurrentUtilization = (requestingDepot.CurrentUtilization ?? 0m) + totalVolume;
                requestingDepot.CurrentWeightUtilization = (requestingDepot.CurrentWeightUtilization ?? 0m) + totalWeight;
            }
        }

        await _unitOfWork.SaveAsync();
    }

    public async Task<PagedResult<SupplyRequestListItem>> GetPagedByDepotsAsync(
        List<int> depotIds,
        string? sourceStatus,
        string? requestingStatus,
        string? roleFilter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var set = _unitOfWork.Set<DepotSupplyRequest>()
            .Include(r => r.RequestingDepot)
            .Include(r => r.SourceDepot)
            .Include(r => r.Items)
                .ThenInclude(i => i.ItemModel);

        IQueryable<DepotSupplyRequest> query = roleFilter switch
        {
            "Requester" => set.Where(r => depotIds.Contains(r.RequestingDepotId)),
            "Source"    => set.Where(r => depotIds.Contains(r.SourceDepotId)),
            _           => set.Where(r => depotIds.Contains(r.RequestingDepotId) || depotIds.Contains(r.SourceDepotId))
        };

        if (sourceStatus != null)
            query = query.Where(r => r.SourceStatus == sourceStatus);

        if (requestingStatus != null)
            query = query.Where(r => r.RequestingStatus == requestingStatus);

        var totalCount = await query.CountAsync(cancellationToken);

        var pagedEntities = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var paged = new PagedResult<DepotSupplyRequest>(pagedEntities, totalCount, pageNumber, pageSize);

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

    public async Task<List<DepotRequestItem>> GetRequestsByDepotIdsAsync(
        List<int> depotIds,
        CancellationToken cancellationToken = default)
    {
        var items = await _unitOfWork.Set<DepotSupplyRequest>()
            .Where(r =>
                depotIds.Contains(r.RequestingDepotId)
                || depotIds.Contains(r.SourceDepotId))
            .Select(r => new DepotRequestItem
            {
                Id                  = r.Id,
                RequestingDepotId   = r.RequestingDepotId,
                RequestingDepotName = r.RequestingDepot != null ? r.RequestingDepot.Name : null,
                SourceDepotId       = r.SourceDepotId,
                SourceDepotName     = r.SourceDepot != null ? r.SourceDepot.Name : null,
                PriorityLevel       = r.PriorityLevel,
                SourceStatus        = r.SourceStatus,
                RequestingStatus    = r.RequestingStatus,
                CreatedAt           = r.CreatedAt,
                AutoRejectAt        = r.AutoRejectAt,
                ShippedAt           = r.ShippedAt,
                CompletedAt         = r.CompletedAt
            })
            .ToListAsync(cancellationToken);

        return items;
    }

    private static SupplyRequestPriorityLevel ParsePriorityLevel(string? priorityLevel)
        => Enum.TryParse<SupplyRequestPriorityLevel>(priorityLevel, true, out var parsed)
            ? parsed
            : SupplyRequestPriorityLevel.Medium;
}
