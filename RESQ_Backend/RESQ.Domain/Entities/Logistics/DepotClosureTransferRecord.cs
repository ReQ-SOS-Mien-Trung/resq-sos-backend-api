using RESQ.Domain.Entities.Logistics.Exceptions;

namespace RESQ.Domain.Entities.Logistics;

/// <summary>
/// Domain entity theo dõi quá trình chuyển hàng khi đóng kho.
/// Vòng đời: AwaitingPreparation -> Preparing (source) -> Shipping (source) -> Completed (source) -> Received (target).
/// </summary>
public class DepotClosureTransferRecord
{
    public int Id { get; private set; }
    public int ClosureId { get; private set; }
    public int SourceDepotId { get; private set; }
    public int TargetDepotId { get; private set; }

    /// <see cref="RESQ.Domain.Enum.Logistics.DepotClosureTransferStatus"/>
    public string Status { get; private set; } = "AwaitingPreparation";

    public DateTime CreatedAt { get; private set; }

    // -- Source side --
    public DateTime? ShippedAt { get; private set; }
    public Guid? ShippedBy { get; private set; }
    public string? ShipNote { get; private set; }

    // -- Target side --
    public DateTime? ReceivedAt { get; private set; }
    public Guid? ReceivedBy { get; private set; }
    public string? ReceiveNote { get; private set; }

    // -- Snapshot --
    public int SnapshotConsumableUnits { get; private set; }
    public int SnapshotReusableUnits { get; private set; }

    // -- Cancel --
    public DateTime? CancelledAt { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public string? CancellationReason { get; private set; }

    private DepotClosureTransferRecord() { }

    public static DepotClosureTransferRecord Create(
        int closureId,
        int sourceDepotId,
        int targetDepotId,
        int snapshotConsumableUnits,
        int snapshotReusableUnits)
    {
        return new DepotClosureTransferRecord
        {
            ClosureId = closureId,
            SourceDepotId = sourceDepotId,
            TargetDepotId = targetDepotId,
            Status = "AwaitingPreparation",
            CreatedAt = DateTime.UtcNow,
            SnapshotConsumableUnits = snapshotConsumableUnits,
            SnapshotReusableUnits = snapshotReusableUnits
        };
    }

    /// <summary>Reconstruct từ EF entity.</summary>
    public static DepotClosureTransferRecord FromPersistence(
        int id, int closureId, int sourceDepotId, int targetDepotId,
        string status, DateTime createdAt,
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

    public void MarkPreparing(Guid preparedBy, string? note = null)
    {
        if (Status != "AwaitingPreparation")
            throw new DepotClosingException(
                $"Không thể chuyển sang Preparing. Trạng thái hiện tại: {Status}.");

        Status = "Preparing";
        ShippedBy = preparedBy;
        ShipNote = note;
    }

    public void MarkShipping(Guid shippedBy, string? note = null)
    {
        if (Status != "Preparing")
            throw new DepotClosingException(
                $"Không thể bắt đầu vận chuyển. Trạng thái hiện tại: {Status}.");

        Status = "Shipping";
        ShippedAt = DateTime.UtcNow;
        ShippedBy = shippedBy;
        ShipNote = note;
    }

    public void MarkCompleted(Guid completedBy, string? note = null)
    {
        if (Status != "Shipping")
            throw new DepotClosingException(
                $"Không thể xác nhận hoàn tất giao hàng. Trạng thái hiện tại: {Status}.");

        Status = "Completed";
        ShipNote = string.IsNullOrWhiteSpace(note) ? ShipNote : note;
        ShippedBy = completedBy;
    }

    public void MarkReceived(Guid receivedBy, string? note = null)
    {
        if (Status != "Completed")
            throw new DepotClosingException(
                $"Không thể xác nhận nhận hàng. Trạng thái hiện tại: {Status}. Kho nguồn phải xác nhận hoàn tất giao hàng trước.");

        Status = "Received";
        ReceivedAt = DateTime.UtcNow;
        ReceivedBy = receivedBy;
        ReceiveNote = note;
    }

    /// <summary>Chỉ cho hủy trước khi hàng rời kho nguồn.</summary>
    public void Cancel(Guid cancelledBy, string? reason = null)
    {
        if (Status != "AwaitingPreparation" && Status != "Preparing")
            throw new DepotClosingException(
                $"Không thể hủy transfer khi hàng đã rời kho hoặc đang chờ nhận. Trạng thái hiện tại: {Status}.");

        Status = "Cancelled";
        CancelledAt = DateTime.UtcNow;
        CancelledBy = cancelledBy;
        CancellationReason = reason;
    }
}
