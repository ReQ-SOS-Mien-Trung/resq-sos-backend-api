using RESQ.Domain.Enum.Logistics;

namespace RESQ.Domain.Entities.Logistics;

/// <summary>
/// Audit record for a depot closure session.
/// </summary>
public class DepotClosureRecord
{
    public int Id { get; private set; }
    public int DepotId { get; private set; }
    public Guid InitiatedBy { get; private set; }
    public DateTime InitiatedAt { get; private set; }

    // Previous depot status so cancellation can restore it.
    public DepotStatus PreviousStatus { get; private set; }

    public string CloseReason { get; private set; } = string.Empty;
    public DepotClosureStatus Status { get; private set; }

    public int SnapshotConsumableUnits { get; private set; }
    public int SnapshotReusableUnits { get; private set; }

    public int? ActualConsumableUnits { get; private set; }
    public int? ActualReusableUnits { get; private set; }
    public string? DriftNote { get; private set; }

    public int TotalConsumableRows { get; private set; }
    public int ProcessedConsumableRows { get; private set; }
    public int? LastProcessedInventoryId { get; private set; }
    public int TotalReusableUnits { get; private set; }
    public int ProcessedReusableUnits { get; private set; }
    public int? LastProcessedReusableId { get; private set; }
    public DateTime? LastBatchAt { get; private set; }

    public CloseResolutionType? ResolutionType { get; private set; }
    public int? TargetDepotId { get; private set; }
    public string? ExternalNote { get; private set; }

    public bool ConsumableZeroed { get; private set; }
    public bool ReusableZeroed { get; private set; }

    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }
    public string? FailureReason { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public Guid? CancelledBy { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    public bool IsForced { get; private set; }
    public string? ForceReason { get; private set; }

    public int RowVersion { get; private set; }

    private DepotClosureRecord() { }

    public void SetGeneratedId(int id) => Id = id;

    public static DepotClosureRecord FromEntity(
        int id,
        int depotId,
        Guid initiatedBy,
        DateTime initiatedAt,
        DepotStatus previousStatus,
        string closeReason,
        DepotClosureStatus status,
        int snapshotConsumableUnits,
        int snapshotReusableUnits,
        int? actualConsumableUnits,
        int? actualReusableUnits,
        string? driftNote,
        int totalConsumableRows,
        int processedConsumableRows,
        int? lastProcessedInventoryId,
        int totalReusableUnits,
        int processedReusableUnits,
        int? lastProcessedReusableId,
        DateTime? lastBatchAt,
        CloseResolutionType? resolutionType,
        int? targetDepotId,
        string? externalNote,
        bool consumableZeroed,
        bool reusableZeroed,
        int retryCount,
        int maxRetries,
        string? failureReason,
        DateTime? completedAt,
        Guid? cancelledBy,
        DateTime? cancelledAt,
        string? cancellationReason,
        bool isForced,
        string? forceReason,
        int rowVersion)
    {
        return new DepotClosureRecord
        {
            Id = id,
            DepotId = depotId,
            InitiatedBy = initiatedBy,
            InitiatedAt = initiatedAt,
            PreviousStatus = previousStatus,
            CloseReason = closeReason,
            Status = status,
            SnapshotConsumableUnits = snapshotConsumableUnits,
            SnapshotReusableUnits = snapshotReusableUnits,
            ActualConsumableUnits = actualConsumableUnits,
            ActualReusableUnits = actualReusableUnits,
            DriftNote = driftNote,
            TotalConsumableRows = totalConsumableRows,
            ProcessedConsumableRows = processedConsumableRows,
            LastProcessedInventoryId = lastProcessedInventoryId,
            TotalReusableUnits = totalReusableUnits,
            ProcessedReusableUnits = processedReusableUnits,
            LastProcessedReusableId = lastProcessedReusableId,
            LastBatchAt = lastBatchAt,
            ResolutionType = resolutionType,
            TargetDepotId = targetDepotId,
            ExternalNote = externalNote,
            ConsumableZeroed = consumableZeroed,
            ReusableZeroed = reusableZeroed,
            RetryCount = retryCount,
            MaxRetries = maxRetries,
            FailureReason = failureReason,
            CompletedAt = completedAt,
            CancelledBy = cancelledBy,
            CancelledAt = cancelledAt,
            CancellationReason = cancellationReason,
            IsForced = isForced,
            ForceReason = forceReason,
            RowVersion = rowVersion
        };
    }

    public static DepotClosureRecord Create(
        int depotId,
        Guid initiatedBy,
        string? closeReason,
        DepotStatus previousStatus,
        int snapshotConsumableUnits,
        int snapshotReusableUnits,
        int totalConsumableRows,
        int totalReusableUnits)
    {
        return new DepotClosureRecord
        {
            DepotId = depotId,
            InitiatedBy = initiatedBy,
            InitiatedAt = DateTime.UtcNow,
            PreviousStatus = previousStatus,
            CloseReason = closeReason ?? string.Empty,
            Status = DepotClosureStatus.InProgress,
            SnapshotConsumableUnits = snapshotConsumableUnits,
            SnapshotReusableUnits = snapshotReusableUnits,
            TotalConsumableRows = totalConsumableRows,
            TotalReusableUnits = totalReusableUnits,
            MaxRetries = 5,
            RowVersion = 1
        };
    }

    public void SetTransferResolution(int? targetDepotId = null)
    {
        ResolutionType = CloseResolutionType.TransferToDepot;
        TargetDepotId = targetDepotId;
    }

    public void SetExternalResolution(string? note)
    {
        ResolutionType = CloseResolutionType.ExternalResolution;
        ExternalNote = note;
    }

    public void RecordActualInventory(int actualConsumable, int actualReusable)
    {
        ActualConsumableUnits = actualConsumable;
        ActualReusableUnits = actualReusable;

        var consumableDrift = SnapshotConsumableUnits - actualConsumable;
        if (consumableDrift > 0)
        {
            DriftNote = $"Mission đã tiêu thụ {consumableDrift} đơn vị trong quá trình đóng kho.";
        }
    }

    public void Complete(DateTime completedAt)
    {
        Status = DepotClosureStatus.Completed;
        CompletedAt = completedAt;
        RowVersion++;
    }

    public void RecordFailure(string reason)
    {
        RetryCount++;
        FailureReason = reason;
        Status = RetryCount >= MaxRetries ? DepotClosureStatus.Failed : DepotClosureStatus.InProgress;
        RowVersion++;
    }

    public void Cancel(Guid cancelledBy, DateTime cancelledAt, string reason)
    {
        Status = DepotClosureStatus.Cancelled;
        CancelledBy = cancelledBy;
        CancelledAt = cancelledAt;
        CancellationReason = reason;
        RowVersion++;
    }

    public void UpdateBatchProgress(int processedRows, int lastInventoryId)
    {
        ProcessedConsumableRows = processedRows;
        LastProcessedInventoryId = lastInventoryId;
        LastBatchAt = DateTime.UtcNow;
    }

    public void MarkConsumableZeroed()
    {
        ConsumableZeroed = true;
    }

    public void MarkReusableZeroed()
    {
        ReusableZeroed = true;
    }

    public void MarkTransferPending()
    {
        Status = DepotClosureStatus.TransferPending;
        RowVersion++;
    }
}
