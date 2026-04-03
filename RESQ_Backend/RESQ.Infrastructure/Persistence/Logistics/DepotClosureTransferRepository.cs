using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class DepotClosureTransferRepository(IUnitOfWork unitOfWork)
    : IDepotClosureTransferRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<int> CreateAsync(DepotClosureTransferRecord record, CancellationToken cancellationToken = default)
    {
        var entity = ToEntity(record);
        await _unitOfWork.GetRepository<DepotClosureTransfer>().AddAsync(entity);
        await _unitOfWork.SaveAsync();
        return entity.Id;
    }

    public async Task<DepotClosureTransferRecord?> GetByIdAsync(int transferId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotClosureTransfer>()
            .GetByPropertyAsync(x => x.Id == transferId, tracked: false);
        return entity == null ? null : ToDomain(entity);
    }

    public async Task<DepotClosureTransferRecord?> GetActiveByClosureIdAsync(int closureId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotClosureTransfer>()
            .GetByPropertyAsync(
                x => x.ClosureId == closureId &&
                     x.Status != "Completed" &&
                     x.Status != "Cancelled",
                tracked: false);
        return entity == null ? null : ToDomain(entity);
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
    }

    // ── Mappers ──────────────────────────────────────────────────────────────

    private static DepotClosureTransfer ToEntity(DepotClosureTransferRecord record)
    {
        return new DepotClosureTransfer
        {
            ClosureId = record.ClosureId,
            SourceDepotId = record.SourceDepotId,
            TargetDepotId = record.TargetDepotId,
            Status = record.Status,
            CreatedAt = record.CreatedAt,
            TransferDeadlineAt = record.TransferDeadlineAt,
            SnapshotConsumableUnits = record.SnapshotConsumableUnits,
            SnapshotReusableUnits = record.SnapshotReusableUnits
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
            transferDeadlineAt: entity.TransferDeadlineAt,
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
}
