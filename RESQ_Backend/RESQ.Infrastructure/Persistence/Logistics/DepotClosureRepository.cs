using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Persistence.Context;


namespace RESQ.Infrastructure.Persistence.Logistics;

public class DepotClosureRepository(IUnitOfWork unitOfWork, ResQDbContext dbContext)
    : IDepotClosureRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ResQDbContext _dbContext = dbContext;

    public async Task<int> CreateAsync(DepotClosureRecord record, CancellationToken cancellationToken = default)
    {
        var entity = ToEntity(record);
        await _unitOfWork.GetRepository<DepotClosure>().AddAsync(entity);
        await _unitOfWork.SaveAsync();
        return entity.Id;
    }

    public async Task<DepotClosureRecord?> GetByIdAsync(int closureId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotClosure>()
            .GetByPropertyAsync(x => x.Id == closureId, tracked: false);
        return entity == null ? null : ToDomain(entity);
    }

    public async Task<DepotClosureRecord?> GetActiveClosureByDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotClosure>()
            .GetByPropertyAsync(
                x => x.DepotId == depotId &&
                     (x.Status == "InProgress" || x.Status == "Processing" || x.Status == "TransferPending"),
                tracked: false);
        return entity == null ? null : ToDomain(entity);
    }

    public async Task<DepotClosureRecord?> GetLatestClosureByDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.Set<DepotClosure>()
            .AsNoTracking()
            .Where(x => x.DepotId == depotId)
            .OrderByDescending(x => x.InitiatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null ? null : ToDomain(entity);
    }

    public async Task UpdateAsync(DepotClosureRecord record, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<DepotClosure>();
        var entity = await repo.GetByPropertyAsync(x => x.Id == record.Id, tracked: true);
        if (entity == null) return;

        // Map domain fields back to entity
        entity.Status = record.Status.ToString();
        entity.ResolutionType = record.ResolutionType?.ToString();
        entity.TargetDepotId = record.TargetDepotId;
        entity.ExternalNote = record.ExternalNote;
        entity.ActualConsumableUnits = record.ActualConsumableUnits;
        entity.ActualReusableUnits = record.ActualReusableUnits;
        entity.DriftNote = record.DriftNote;
        entity.ProcessedConsumableRows = record.ProcessedConsumableRows;
        entity.LastProcessedInventoryId = record.LastProcessedInventoryId;
        entity.ProcessedReusableUnits = record.ProcessedReusableUnits;
        entity.LastBatchAt = record.LastBatchAt;
        entity.ConsumableZeroed = record.ConsumableZeroed;
        entity.ReusableZeroed = record.ReusableZeroed;
        entity.RetryCount = record.RetryCount;
        entity.FailureReason = record.FailureReason;
        entity.CompletedAt = record.CompletedAt;
        entity.CancelledBy = record.CancelledBy;
        entity.CancelledAt = record.CancelledAt;
        entity.CancellationReason = record.CancellationReason;
        entity.IsForced = record.IsForced;
        entity.ForceReason = record.ForceReason;
        entity.RowVersion = record.RowVersion;

        await repo.UpdateAsync(entity);
    }

    /// <summary>
    /// Atomic claim: dùng conditional UPDATE để chuyển InProgress -> Processing.
    /// Trả về true nếu thành công, false nếu đã bị claim bởi tiến trình khác.
    /// row_version được tăng lên cùng lúc để CancelDepotClosure không thể dùng
    /// TryForceClaimFromProcessingAsync (vốn kiểm tra row_version) để steal lock
    /// trong khoảng hở khi Resolve đang mid-flight.
    /// </summary>
    public async Task<bool> TryClaimForProcessingAsync(int closureId, CancellationToken cancellationToken = default)
    {
        var affected = await _dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE depot_closures
            SET status = 'Processing', row_version = row_version + 1
            WHERE id = {0} AND status = 'InProgress'
            """,
            closureId);

        return affected > 0;
    }

    /// <summary>
    /// Hoàn tác claim: chuyển Processing -> InProgress để cho phép user retry
    /// sau khi xảy ra lỗi validation (ConflictException / NotFoundException).
    /// </summary>
    public async Task ResetProcessingToInProgressAsync(int closureId, CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE depot_closures
            SET status = 'InProgress'
            WHERE id = {0} AND status = 'Processing'
            """,
            closureId);
    }

    /// <summary>
    /// Tái claim bản ghi bị kẹt tại Processing bằng optimistic concurrency (kiểm tra rowVersion).
    /// An toàn khi nhiều request cùng cố recover đồng thời: chỉ đúng 1 request thành công.
    /// </summary>
    public async Task<bool> TryForceClaimFromProcessingAsync(int closureId, int expectedRowVersion, CancellationToken cancellationToken = default)
    {
        var affected = await _dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE depot_closures
            SET status = 'Processing', row_version = row_version + 1
            WHERE id = {0} AND status = 'Processing' AND row_version = {1}
            """,
            closureId, expectedRowVersion);

        return affected > 0;
    }

    public async Task UpdateProgressAsync(int closureId, int processedRows, int lastInventoryId,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE depot_closures
            SET processed_consumable_rows = {0},
                last_processed_inventory_id = {1},
                last_batch_at = NOW()
            WHERE id = {2}
            """,
            processedRows, lastInventoryId, closureId);
    }

    public async Task<List<DepotClosureListItem>> GetClosuresByDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var relatedTransfers = await (
            from transfer in _dbContext.DepotClosureTransfers.AsNoTracking()
            join targetDepot in _dbContext.Depots.AsNoTracking()
                on transfer.TargetDepotId equals targetDepot.Id
            where transfer.SourceDepotId == depotId || transfer.TargetDepotId == depotId
            select new
            {
                transfer.ClosureId,
                transfer.Id,
                transfer.TargetDepotId,
                TargetDepotName = targetDepot.Name,
                transfer.Status
            })
            .ToListAsync(cancellationToken);

        var targetRelatedClosureIds = relatedTransfers
            .Where(x => x.TargetDepotId == depotId)
            .Select(x => x.ClosureId)
            .Distinct()
            .ToList();

        var items = await BuildClosureListQuery()
            .Where(x => x.DepotId == depotId || targetRelatedClosureIds.Contains(x.Id))
            .OrderByDescending(x => x.InitiatedAt)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.RelatedDepotRole = item.DepotId == depotId ? "SourceDepot" : "TargetDepot";

            var transferSummaries = relatedTransfers
                .Where(x => x.ClosureId == item.Id)
                .OrderBy(x => x.TargetDepotName)
                .ThenBy(x => x.Id)
                .Select(x => new DepotClosureListTransferItem
                {
                    TransferId = x.Id,
                    TargetDepotId = x.TargetDepotId,
                    TargetDepotName = x.TargetDepotName,
                    Status = x.Status
                })
                .ToList();

            item.Transfers = transferSummaries;

            var relevantTransfers = item.RelatedDepotRole == "TargetDepot"
                ? transferSummaries.Where(x => x.TargetDepotId == depotId).ToList()
                : transferSummaries;

            if (relevantTransfers.Count == 1)
            {
                item.TransferId = relevantTransfers[0].TransferId;
                item.TransferStatus = relevantTransfers[0].Status;
            }

            var distinctTargets = relevantTransfers
                .Select(x => new { x.TargetDepotId, x.TargetDepotName })
                .Distinct()
                .Take(2)
                .ToList();

            if (distinctTargets.Count == 1)
            {
                item.TargetDepotId = distinctTargets[0].TargetDepotId;
                item.TargetDepotName = distinctTargets[0].TargetDepotName;
            }
            else if (item.RelatedDepotRole == "TargetDepot")
            {
                item.TargetDepotId = null;
                item.TargetDepotName = null;
            }
        }

        return items;
    }

    public async Task<DepotClosureListItem?> GetClosureDetailAsync(int depotId, int closureId, CancellationToken cancellationToken = default)
    {
        return await BuildClosureListQuery()
            .FirstOrDefaultAsync(
                x => x.Id == closureId && (x.DepotId == depotId || x.TargetDepotId == depotId),
                cancellationToken);
    }

    // â”€â”€ Mappers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static DepotClosure ToEntity(DepotClosureRecord record)
    {
        return new DepotClosure
        {
            DepotId = record.DepotId,
            InitiatedBy = record.InitiatedBy,
            InitiatedAt = record.InitiatedAt,
            PreviousStatus = record.PreviousStatus.ToString(),
            CloseReason = record.CloseReason,
            Status = record.Status.ToString(),
            SnapshotConsumableUnits = record.SnapshotConsumableUnits,
            SnapshotReusableUnits = record.SnapshotReusableUnits,
            TotalConsumableRows = record.TotalConsumableRows,
            TotalReusableUnits = record.TotalReusableUnits,
            MaxRetries = record.MaxRetries,
            RowVersion = record.RowVersion
        };
    }

    private static DepotClosureRecord ToDomain(DepotClosure entity)
    {
        Enum.TryParse<DepotClosureStatus>(entity.Status, out var status);
        Enum.TryParse<DepotStatus>(entity.PreviousStatus, out var prevStatus);
        Enum.TryParse<CloseResolutionType>(entity.ResolutionType ?? "", out var resolutionType);
        return DepotClosureRecord.FromEntity(
            id: entity.Id,
            depotId: entity.DepotId,
            initiatedBy: entity.InitiatedBy,
            initiatedAt: entity.InitiatedAt,
            previousStatus: prevStatus,
            closeReason: entity.CloseReason,
            status: status,
            snapshotConsumableUnits: entity.SnapshotConsumableUnits,
            snapshotReusableUnits: entity.SnapshotReusableUnits,
            actualConsumableUnits: entity.ActualConsumableUnits,
            actualReusableUnits: entity.ActualReusableUnits,
            driftNote: entity.DriftNote,
            totalConsumableRows: entity.TotalConsumableRows,
            processedConsumableRows: entity.ProcessedConsumableRows,
            lastProcessedInventoryId: entity.LastProcessedInventoryId,
            totalReusableUnits: entity.TotalReusableUnits,
            processedReusableUnits: entity.ProcessedReusableUnits,
            lastProcessedReusableId: entity.LastProcessedReusableId,
            lastBatchAt: entity.LastBatchAt,
            resolutionType: entity.ResolutionType != null ? resolutionType : null,
            targetDepotId: entity.TargetDepotId,
            externalNote: entity.ExternalNote,
            consumableZeroed: entity.ConsumableZeroed,
            reusableZeroed: entity.ReusableZeroed,
            retryCount: entity.RetryCount,
            maxRetries: entity.MaxRetries,
            failureReason: entity.FailureReason,
            completedAt: entity.CompletedAt,
            cancelledBy: entity.CancelledBy,
            cancelledAt: entity.CancelledAt,
            cancellationReason: entity.CancellationReason,
            isForced: entity.IsForced,
            forceReason: entity.ForceReason,
            rowVersion: entity.RowVersion);
    }

    private IQueryable<DepotClosureListItem> BuildClosureListQuery()
    {
        return
            from closure in _dbContext.DepotClosures.AsNoTracking()
            join initiator in _dbContext.Set<User>().AsNoTracking()
                on closure.InitiatedBy equals initiator.Id into initGroup
            from initiator in initGroup.DefaultIfEmpty()
            join canceller in _dbContext.Set<User>().AsNoTracking()
                on closure.CancelledBy equals canceller.Id into cancelGroup
            from canceller in cancelGroup.DefaultIfEmpty()
            join targetDepot in _dbContext.Depots.AsNoTracking()
                on closure.TargetDepotId equals targetDepot.Id into targetGroup
            from targetDepot in targetGroup.DefaultIfEmpty()
            select new DepotClosureListItem
            {
                Id = closure.Id,
                DepotId = closure.DepotId,
                Status = Enum.Parse<DepotClosureStatus>(closure.Status),
                PreviousStatus = Enum.Parse<DepotStatus>(closure.PreviousStatus),
                CloseReason = closure.CloseReason,
                ResolutionType = closure.ResolutionType != null
                    ? Enum.Parse<CloseResolutionType>(closure.ResolutionType!)
                    : (CloseResolutionType?)null,
                TargetDepotId = closure.TargetDepotId,
                TargetDepotName = targetDepot != null ? targetDepot.Name : null,
                ExternalNote = closure.ExternalNote,
                InitiatedBy = closure.InitiatedBy,
                InitiatedByFullName = initiator != null
                    ? (initiator.LastName + " " + initiator.FirstName).Trim()
                    : null,
                CancelledBy = closure.CancelledBy,
                CancelledByFullName = canceller != null
                    ? (canceller.LastName + " " + canceller.FirstName).Trim()
                    : null,
                CancellationReason = closure.CancellationReason,
                SnapshotConsumableUnits = closure.SnapshotConsumableUnits,
                SnapshotReusableUnits = closure.SnapshotReusableUnits,
                InitiatedAt = closure.InitiatedAt,
                CompletedAt = closure.CompletedAt,
                CancelledAt = closure.CancelledAt,
            };
    }
}
