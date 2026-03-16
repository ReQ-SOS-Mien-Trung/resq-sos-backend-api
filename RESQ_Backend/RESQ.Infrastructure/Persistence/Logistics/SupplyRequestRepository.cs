using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class SupplyRequestRepository(IUnitOfWork unitOfWork) : ISupplyRequestRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<int> CreateAsync(
        int requestingDepotId,
        int sourceDepotId,
        List<(int ReliefItemId, int Quantity)> items,
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
            ReliefItemId         = i.ReliefItemId,
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
            Items             = entity.Items.Select(i => (i.ReliefItemId, i.Quantity)).ToList()
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
            case "Shipped":
                entity.ShippedAt = now;
                break;
            case "Completed":
                entity.CompletedAt = now;
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

    public async Task TransferOutAsync(
        int sourceDepotId,
        List<(int ReliefItemId, int Quantity)> items,
        int supplyRequestId,
        Guid performedBy,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var (reliefItemId, quantity) in items)
        {
            var inventory = await _unitOfWork.GetRepository<DepotSupplyInventory>()
                .GetByPropertyAsync(x => x.DepotId == sourceDepotId && x.ReliefItemId == reliefItemId, tracked: true)
                ?? throw new BadRequestException($"Kho nguồn không có vật tư #{reliefItemId} trong tồn kho.");

            var available = (inventory.Quantity ?? 0) - (inventory.ReservedQuantity ?? 0);
            if (available < quantity)
                throw new BadRequestException($"Vật tư #{reliefItemId}: tồn kho khả dụng ({available}) không đủ so với yêu cầu ({quantity}).");

            inventory.Quantity      = (inventory.Quantity ?? 0) - quantity;
            inventory.LastStockedAt = now;

            await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
            {
                DepotSupplyInventoryId = inventory.Id,
                ActionType             = "TransferOut",
                QuantityChange         = quantity,
                SourceType             = "SupplyRequest",
                SourceId               = supplyRequestId,
                PerformedBy            = performedBy,
                Note                   = $"Xuất tiếp tế cho yêu cầu #{supplyRequestId}",
                CreatedAt              = now
            });
        }

        await _unitOfWork.SaveAsync();
    }

    public async Task TransferInAsync(
        int requestingDepotId,
        List<(int ReliefItemId, int Quantity)> items,
        int supplyRequestId,
        Guid performedBy,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var (reliefItemId, quantity) in items)
        {
            var inventory = await _unitOfWork.GetRepository<DepotSupplyInventory>()
                .GetByPropertyAsync(x => x.DepotId == requestingDepotId && x.ReliefItemId == reliefItemId, tracked: true);

            if (inventory == null)
            {
                inventory = new DepotSupplyInventory
                {
                    DepotId          = requestingDepotId,
                    ReliefItemId     = reliefItemId,
                    Quantity         = 0,
                    ReservedQuantity = 0,
                    LastStockedAt    = now
                };
                await _unitOfWork.GetRepository<DepotSupplyInventory>().AddAsync(inventory);
                await _unitOfWork.SaveAsync();
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
                Note                   = $"Nhận tiếp tế từ yêu cầu #{supplyRequestId}",
                CreatedAt              = now
            });
        }

        await _unitOfWork.SaveAsync();
    }
}
