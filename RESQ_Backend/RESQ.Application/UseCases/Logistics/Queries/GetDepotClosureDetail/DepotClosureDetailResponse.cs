using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotClosureDetail;

public class DepotClosureDetailResponse
{
    public int Id { get; set; }
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string CloseReason { get; set; } = string.Empty;
    public string? ResolutionType { get; set; }
    public int? TargetDepotId { get; set; }
    public string? TargetDepotName { get; set; }
    public string? ExternalNote { get; set; }
    public Guid InitiatedBy { get; set; }
    public string? InitiatedByFullName { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancelledByFullName { get; set; }
    public string? CancellationReason { get; set; }
    public int SnapshotConsumableUnits { get; set; }
    public int SnapshotReusableUnits { get; set; }
    public int? ActualConsumableUnits { get; set; }
    public int? ActualReusableUnits { get; set; }
    public string? DriftNote { get; set; }
    public string? FailureReason { get; set; }
    public bool IsForced { get; set; }
    public string? ForceReason { get; set; }
    public DateTime InitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public bool HasOpenTransfers { get; set; }
    public bool HasRemainingItems { get; set; }
    public int RemainingItemCount { get; set; }
    public bool HasTransferableRemainingItems { get; set; }
    public int TransferableRemainingItemCount { get; set; }
    public int TransferableRemainingUnitCount { get; set; }
    public int BlockedRemainingItemCount { get; set; }
    public int BlockedRemainingUnitCount { get; set; }
    public bool HasClosingBlockers { get; set; }
    public int ReservedConsumableItemCount { get; set; }
    public int ReservedConsumableUnitCount { get; set; }
    public int NonAvailableReusableItemModelCount { get; set; }
    public int NonAvailableReusableUnitCount { get; set; }
    public bool CanSelectResolutionOption { get; set; }
    public bool CanConfirmClose { get; set; }
    public bool CanDownloadExternalTemplate { get; set; }
    public bool CanUploadExternalResolution { get; set; }
    public bool HasTransferRecords { get; set; }
    public bool HasExternalResolutionRecords { get; set; }
    public List<ClosureInventoryItemDto> RemainingInventoryItems { get; set; } = [];
    public List<DepotClosureTransferDetailDto> TransferDetails { get; set; } = [];
    public List<DepotClosureExternalItemDetailResponse> ExternalItems { get; set; } = [];
}

public class DepotClosureTransferDetailDto
{
    public int Id { get; set; }
    public int ClosureId { get; set; }
    public int SourceDepotId { get; set; }
    public string? SourceDepotName { get; set; }
    public int TargetDepotId { get; set; }
    public string? TargetDepotName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int SnapshotConsumableUnits { get; set; }
    public int SnapshotReusableUnits { get; set; }
    public DateTime? ShippedAt { get; set; }
    public Guid? ShippedBy { get; set; }
    public string? ShipNote { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public Guid? ReceivedBy { get; set; }
    public string? ReceiveNote { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
    public List<DepotClosureTransferItemDetailDto> Items { get; set; } = [];
}

public class DepotClosureTransferItemDetailDto
{
    public int ItemModelId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int Quantity { get; set; }
    public int? LotId { get; set; }
    public int? ReusableItemId { get; set; }
    public string? SerialNumber { get; set; }
}

public class DepotClosureExternalItemDetailResponse
{
    public int Id { get; set; }
    public int? ItemModelId { get; set; }
    public int? LotId { get; set; }
    public int? ReusableItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string? SerialNumber { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
    public string HandlingMethod { get; set; } = string.Empty;
    public string HandlingMethodDisplay { get; set; } = string.Empty;
    public string? Recipient { get; set; }
    public string? Note { get; set; }
    public string? ImageUrl { get; set; }
    public Guid ProcessedBy { get; set; }
    public DateTime ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
