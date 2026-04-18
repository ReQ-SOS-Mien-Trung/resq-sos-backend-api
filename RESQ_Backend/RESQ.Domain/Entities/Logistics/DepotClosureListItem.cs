using RESQ.Domain.Enum.Logistics;

namespace RESQ.Domain.Entities.Logistics;

/// <summary>
/// Projection dùng để trả về danh sách phiên đóng kho cho FE.
/// Không phải full domain object - chỉ chứa dữ liệu cần thiết để render bảng.
/// </summary>
public class DepotClosureListItem
{
    public int Id { get; set; }
    public int DepotId { get; set; }
    public string RelatedDepotRole { get; set; } = "SourceDepot";

    public DepotClosureStatus Status { get; set; }
    public DepotStatus PreviousStatus { get; set; }

    public string CloseReason { get; set; } = string.Empty;
    public CloseResolutionType? ResolutionType { get; set; }

    // Kho đích nếu chọn TransferToDepot
    public int? TargetDepotId { get; set; }
    public string? TargetDepotName { get; set; }

    // Ghi chú xử lý bên ngoài nếu chọn ExternalResolution
    public string? ExternalNote { get; set; }

    // Ai khởi tạo
    public Guid InitiatedBy { get; set; }
    public string? InitiatedByFullName { get; set; }

    // Ai huỷ (nếu có)
    public Guid? CancelledBy { get; set; }
    public string? CancelledByFullName { get; set; }
    public string? CancellationReason { get; set; }

    // Snapshot tồn kho lúc khởi tạo
    public int SnapshotConsumableUnits { get; set; }
    public int SnapshotReusableUnits { get; set; }

    // Timestamps
    public DateTime InitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    // Transfer tóm tắt (nếu có)
    public int? TransferId { get; set; }
    public string? TransferStatus { get; set; }
    public List<DepotClosureListTransferItem> Transfers { get; set; } = [];
}

public class DepotClosureListTransferItem
{
    public int TransferId { get; set; }
    public int TargetDepotId { get; set; }
    public string TargetDepotName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
