namespace RESQ.Application.UseCases.Logistics.Queries.GetClosureTransfer;

public class ClosureTransferResponse
{
    public int Id { get; set; }
    public int ClosureId { get; set; }
    public int SourceDepotId { get; set; }
    public int TargetDepotId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public int SnapshotConsumableUnits { get; set; }
    public int SnapshotReusableUnits { get; set; }

    // Source side
    public DateTime? ShippedAt { get; set; }
    public Guid? ShippedBy { get; set; }
    public string? ShipNote { get; set; }

    // Target side
    public DateTime? ReceivedAt { get; set; }
    public Guid? ReceivedBy { get; set; }
    public string? ReceiveNote { get; set; }

    // Cancel
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
    public List<ClosureTransferItemResponse> Items { get; set; } = [];
}

public class ClosureTransferItemResponse
{
    public int ItemModelId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int Quantity { get; set; }
}
