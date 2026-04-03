using RESQ.Domain.Enum.Logistics;

namespace RESQ.Domain.Entities.Logistics;

/// <summary>
/// Bản ghi theo dõi và kiểm toán quá trình đóng kho.
/// Mỗi lần admin khởi tạo đóng kho sẽ tạo một bản ghi này.
/// </summary>
public class DepotClosureRecord
{
    public int Id { get; private set; }
    public int DepotId { get; private set; }
    public Guid InitiatedBy { get; private set; }
    public DateTime InitiatedAt { get; private set; }
    public DateTime ClosingTimeoutAt { get; private set; }

    // Trạng thái trước khi đóng — dùng để khôi phục nếu huỷ/timeout
    public DepotStatus PreviousStatus { get; private set; }

    // Lý do đóng kho (bắt buộc)
    public string CloseReason { get; private set; } = string.Empty;

    // Trạng thái của quá trình đóng
    public DepotClosureStatus Status { get; private set; }

    // Snapshot số lượng tồn kho lúc initiate — phát hiện drift ở Phase 2
    public int SnapshotConsumableUnits { get; private set; }
    public int SnapshotReusableUnits { get; private set; }

    // Actual values khi resolve (có thể khác snapshot do mission đang chạy)
    public int? ActualConsumableUnits { get; private set; }
    public int? ActualReusableUnits { get; private set; }
    public string? DriftNote { get; private set; }

    // Progress tracking cho batch operations
    public int TotalConsumableRows { get; private set; }
    public int ProcessedConsumableRows { get; private set; }
    public int? LastProcessedInventoryId { get; private set; }
    public int TotalReusableUnits { get; private set; }
    public int ProcessedReusableUnits { get; private set; }
    public int? LastProcessedReusableId { get; private set; }
    public DateTime? LastBatchAt { get; private set; }

    // Cách giải quyết hàng tồn
    public CloseResolutionType? ResolutionType { get; private set; }

    // Option 1: Chuyển sang kho khác
    public int? TargetDepotId { get; private set; }

    // Option 2: Xử lý bên ngoài
    public string? ExternalNote { get; private set; }

    // Idempotency flags
    public bool ConsumableZeroed { get; private set; }
    public bool ReusableZeroed { get; private set; }

    // Retry management
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }
    public string? FailureReason { get; private set; }

    // Completion
    public DateTime? CompletedAt { get; private set; }

    // Cancellation
    public Guid? CancelledBy { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    // Force close
    public bool IsForced { get; private set; }
    public string? ForceReason { get; private set; }

    // Optimistic concurrency
    public int RowVersion { get; private set; }

    private DepotClosureRecord() { }

    /// <summary>
    /// Khôi phục domain object từ dữ liệu DB (dùng bởi repository mapper).
    /// </summary>
    public static DepotClosureRecord FromEntity(
        int id, int depotId, Guid initiatedBy, DateTime initiatedAt,
        DateTime closingTimeoutAt, DepotStatus previousStatus, string closeReason,
        DepotClosureStatus status, int snapshotConsumableUnits, int snapshotReusableUnits,
        int? actualConsumableUnits, int? actualReusableUnits, string? driftNote,
        int totalConsumableRows, int processedConsumableRows, int? lastProcessedInventoryId,
        int totalReusableUnits, int processedReusableUnits, int? lastProcessedReusableId,
        DateTime? lastBatchAt, CloseResolutionType? resolutionType, int? targetDepotId,
        string? externalNote,
        bool consumableZeroed, bool reusableZeroed, int retryCount, int maxRetries,
        string? failureReason, DateTime? completedAt, Guid? cancelledBy, DateTime? cancelledAt,
        string? cancellationReason, bool isForced, string? forceReason, int rowVersion)
    {
        return new DepotClosureRecord
        {
            Id = id,
            DepotId = depotId,
            InitiatedBy = initiatedBy,
            InitiatedAt = initiatedAt,
            ClosingTimeoutAt = closingTimeoutAt,
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

    /// <summary>
    /// Tạo bản ghi đóng kho mới khi admin nhấn "Đóng kho".
    /// </summary>
    public static DepotClosureRecord Create(
        int depotId,
        Guid initiatedBy,
        string closeReason,
        DepotStatus previousStatus,
        int snapshotConsumableUnits,
        int snapshotReusableUnits,
        int totalConsumableRows,
        int totalReusableUnits,
        DateTime? timeoutAt = null)
    {
        return new DepotClosureRecord
        {
            DepotId = depotId,
            InitiatedBy = initiatedBy,
            InitiatedAt = DateTime.UtcNow,
            ClosingTimeoutAt = timeoutAt ?? DateTime.UtcNow.AddMinutes(30),
            PreviousStatus = previousStatus,
            CloseReason = closeReason,
            Status = DepotClosureStatus.InProgress,
            SnapshotConsumableUnits = snapshotConsumableUnits,
            SnapshotReusableUnits = snapshotReusableUnits,
            TotalConsumableRows = totalConsumableRows,
            TotalReusableUnits = totalReusableUnits,
            MaxRetries = 5,
            RowVersion = 1
        };
    }

    /// <summary>
    /// Đặt cách giải quyết: chuyển sang kho khác.
    /// </summary>
    public void SetTransferResolution(int targetDepotId)
    {
        ResolutionType = CloseResolutionType.TransferToDepot;
        TargetDepotId = targetDepotId;
    }

    /// <summary>
    /// Đặt cách giải quyết: xử lý bên ngoài — ghi nhận ghi chú mô tả cách xử lý.
    /// </summary>
    public void SetExternalResolution(string? note)
    {
        ResolutionType = CloseResolutionType.ExternalResolution;
        ExternalNote = note;
    }

    /// <summary>
    /// Ghi nhận actual inventory values ở Phase 2 và note nếu có drift.
    /// </summary>
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

    /// <summary>
    /// Hoàn tất đóng kho thành công.
    /// </summary>
    public void Complete(DateTime completedAt)
    {
        Status = DepotClosureStatus.Completed;
        CompletedAt = completedAt;
        RowVersion++;
    }

    /// <summary>
    /// Đánh dấu thất bại, tăng retry count.
    /// </summary>
    public void RecordFailure(string reason)
    {
        RetryCount++;
        FailureReason = reason;
        if (RetryCount >= MaxRetries)
        {
            Status = DepotClosureStatus.Failed;
        }
        else
        {
            Status = DepotClosureStatus.InProgress; // Cho phép retry
        }
        RowVersion++;
    }

    /// <summary>
    /// Huỷ yêu cầu đóng kho — kho sẽ được khôi phục về trạng thái cũ.
    /// </summary>
    public void Cancel(Guid cancelledBy, DateTime cancelledAt, string reason)
    {
        Status = DepotClosureStatus.Cancelled;
        CancelledBy = cancelledBy;
        CancelledAt = cancelledAt;
        CancellationReason = reason;
        RowVersion++;
    }

    /// <summary>
    /// Đánh dấu timeout — được gọi bởi background daemon.
    /// </summary>
    public void MarkTimedOut()
    {
        Status = DepotClosureStatus.TimedOut;
        RowVersion++;
    }

    /// <summary>
    /// Cập nhật tiến độ batch processing.
    /// </summary>
    public void UpdateBatchProgress(int processedRows, int lastInventoryId)
    {
        ProcessedConsumableRows = processedRows;
        LastProcessedInventoryId = lastInventoryId;
        LastBatchAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Đánh dấu consumable inventory đã được xử lý xong.
    /// </summary>
    public void MarkConsumableZeroed()
    {
        ConsumableZeroed = true;
    }

    /// <summary>
    /// Đánh dấu reusable items đã được xử lý xong.
    /// </summary>
    public void MarkReusableZeroed()
    {
        ReusableZeroed = true;
    }

    /// <summary>
    /// Sau khi admin chọn kho đích và tạo transfer record, đưa trạng thái về InProgress
    /// để cả 2 quản lý kho có thể tương tác (xác nhận xuất/nhận hàng).
    /// </summary>
    public void ResetToInProgress()
    {
        Status = DepotClosureStatus.InProgress;
        RowVersion++;
    }
}
