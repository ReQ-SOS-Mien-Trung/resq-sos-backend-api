namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotClosures;

public class DepotClosureDto
{
    public int Id { get; set; }
    public int DepotId { get; set; }

    /// <summary>Trạng thái của phiên đóng kho: InProgress | Completed | Cancelled | TimedOut</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Trạng thái kho trước khi đóng: Available | Full</summary>
    public string PreviousStatus { get; set; } = string.Empty;

    /// <summary>Lý do đóng kho do admin nhập khi initiate.</summary>
    public string CloseReason { get; set; } = string.Empty;

    /// <summary>Cách xử lý hàng tồn: TransferToDepot | ExternalResolution | null (nếu kho rỗng)</summary>
    public string? ResolutionType { get; set; }

    /// <summary>Kho đích nhận hàng (khi ResolutionType = TransferToDepot).</summary>
    public int? TargetDepotId { get; set; }
    public string? TargetDepotName { get; set; }

    /// <summary>Ghi chú xử lý bên ngoài (khi ResolutionType = ExternalResolution).</summary>
    public string? ExternalNote { get; set; }

    public Guid InitiatedBy { get; set; }
    public string? InitiatedByFullName { get; set; }

    public Guid? CancelledBy { get; set; }
    public string? CancelledByFullName { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>Số lượng vật tư tiêu hao (consumable) trong kho lúc bắt đầu đóng.</summary>
    public int SnapshotConsumableUnits { get; set; }

    /// <summary>Số lượng thiết bị tái sử dụng (reusable) trong kho lúc bắt đầu đóng.</summary>
    public int SnapshotReusableUnits { get; set; }

    public DateTime InitiatedAt { get; set; }
    public DateTime ClosingTimeoutAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// Tóm tắt lệnh chuyển hàng nếu có (khi ResolutionType = TransferToDepot).
    /// null nếu kho rỗng hoặc chọn ExternalResolution.
    /// </summary>
    public TransferSummaryDto? Transfer { get; set; }
}

public class TransferSummaryDto
{
    public int TransferId { get; set; }

    /// <summary>
    /// Trạng thái chuyển hàng:
    /// AwaitingShipment → Preparing → Shipping → Completed | Cancelled
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
