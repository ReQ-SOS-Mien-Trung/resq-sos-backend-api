using RESQ.Domain.Entities.Logistics.Exceptions;

namespace RESQ.Domain.Entities.Logistics;

/// <summary>
/// Domain entity theo dõi quá trình chuyển hàng khi đóng kho.
/// Vòng đời: AwaitingShipment → Shipping (source manager) → Completed (target manager).
/// </summary>
public class DepotClosureTransferRecord
{
    public int Id { get; private set; }
    public int ClosureId { get; private set; }
    public int SourceDepotId { get; private set; }
    public int TargetDepotId { get; private set; }

    /// <see cref="RESQ.Domain.Enum.Logistics.DepotClosureTransferStatus"/>
    public string Status { get; private set; } = "AwaitingShipment";

    public DateTime CreatedAt { get; private set; }

    /// <summary>Thời hạn hoàn thành chuyển hàng — mặc định 48 giờ kể từ lúc tạo.</summary>
    public DateTime TransferDeadlineAt { get; private set; }

    // ── Source side ──
    public DateTime? ShippedAt { get; private set; }
    public Guid? ShippedBy { get; private set; }
    public string? ShipNote { get; private set; }

    // ── Target side ──
    public DateTime? ReceivedAt { get; private set; }
    public Guid? ReceivedBy { get; private set; }
    public string? ReceiveNote { get; private set; }

    // ── Snapshot ──
    public int SnapshotConsumableUnits { get; private set; }
    public int SnapshotReusableUnits { get; private set; }

    // ── Cancel ──
    public DateTime? CancelledAt { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public string? CancellationReason { get; private set; }

    private DepotClosureTransferRecord() { }

    // ─────────────────────────────────────────────────────────────────────────
    // Factory
    // ─────────────────────────────────────────────────────────────────────────

    public static DepotClosureTransferRecord Create(
        int closureId,
        int sourceDepotId,
        int targetDepotId,
        int snapshotConsumableUnits,
        int snapshotReusableUnits,
        DateTime? deadlineOverride = null)
    {
        return new DepotClosureTransferRecord
        {
            ClosureId = closureId,
            SourceDepotId = sourceDepotId,
            TargetDepotId = targetDepotId,
            Status = "AwaitingShipment",
            CreatedAt = DateTime.UtcNow,
            TransferDeadlineAt = deadlineOverride ?? DateTime.UtcNow.AddHours(48),
            SnapshotConsumableUnits = snapshotConsumableUnits,
            SnapshotReusableUnits = snapshotReusableUnits
        };
    }

    /// <summary>Reconstruct từ EF entity (dùng trong Repository.FromEntity).</summary>
    public static DepotClosureTransferRecord FromPersistence(
        int id, int closureId, int sourceDepotId, int targetDepotId,
        string status, DateTime createdAt, DateTime transferDeadlineAt,
        DateTime? shippedAt, Guid? shippedBy, string? shipNote,
        DateTime? receivedAt, Guid? receivedBy, string? receiveNote,
        int snapshotConsumableUnits, int snapshotReusableUnits,
        DateTime? cancelledAt, Guid? cancelledBy, string? cancellationReason)
    {
        return new DepotClosureTransferRecord
        {
            Id = id,
            ClosureId = closureId,
            SourceDepotId = sourceDepotId,
            TargetDepotId = targetDepotId,
            Status = status,
            CreatedAt = createdAt,
            TransferDeadlineAt = transferDeadlineAt,
            ShippedAt = shippedAt,
            ShippedBy = shippedBy,
            ShipNote = shipNote,
            ReceivedAt = receivedAt,
            ReceivedBy = receivedBy,
            ReceiveNote = receiveNote,
            SnapshotConsumableUnits = snapshotConsumableUnits,
            SnapshotReusableUnits = snapshotReusableUnits,
            CancelledAt = cancelledAt,
            CancelledBy = cancelledBy,
            CancellationReason = cancellationReason
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // State transitions
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Kho nguồn xác nhận đã xuất hàng → Shipping.</summary>
    public void MarkShipped(Guid shippedBy, string? note = null)
    {
        if (Status != "AwaitingShipment")
            throw new DepotClosingException(
                $"Không thể chuyển sang Shipping — trạng thái hiện tại: {Status}.");

        Status = "Shipping";
        ShippedAt = DateTime.UtcNow;
        ShippedBy = shippedBy;
        ShipNote = note;
    }

    /// <summary>Kho đích xác nhận đã nhận hàng → Completed.</summary>
    public void MarkReceived(Guid receivedBy, string? note = null)
    {
        if (Status != "Shipping")
            throw new DepotClosingException(
                $"Không thể xác nhận nhận hàng — trạng thái hiện tại: {Status}.");

        Status = "Completed";
        ReceivedAt = DateTime.UtcNow;
        ReceivedBy = receivedBy;
        ReceiveNote = note;
    }

    /// <summary>Huỷ quá trình chuyển hàng (chỉ khi chưa nhận).</summary>
    public void Cancel(Guid cancelledBy, string? reason = null)
    {
        if (Status == "Completed")
            throw new DepotClosingException("Không thể huỷ — quá trình chuyển hàng đã hoàn tất.");

        Status = "Cancelled";
        CancelledAt = DateTime.UtcNow;
        CancelledBy = cancelledBy;
        CancellationReason = reason;
    }
}
