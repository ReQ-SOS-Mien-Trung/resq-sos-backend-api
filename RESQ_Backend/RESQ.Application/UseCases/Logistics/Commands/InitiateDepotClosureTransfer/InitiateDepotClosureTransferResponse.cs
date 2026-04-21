namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosureTransfer;

public class InitiateDepotClosureTransferResponse
{
    public int ClosureId { get; set; }
    public int SourceDepotId { get; set; }
    public string SourceDepotName { get; set; } = string.Empty;
    public List<InitiateDepotClosureTransferSummaryDto> Transfers { get; set; } = [];
    public int ReusableItemsSkipped { get; set; }
    public bool HasRemainingItems { get; set; }
    public List<InitiateDepotClosureTransferRemainingItemDto> RemainingItems { get; set; } = [];
    public string Message { get; set; } = string.Empty;
}

public class InitiateDepotClosureTransferSummaryDto
{
    public int TransferId { get; set; }
    public int TargetDepotId { get; set; }
    public string TargetDepotName { get; set; } = string.Empty;
    public string TransferStatus { get; set; } = string.Empty;
    public int SnapshotConsumableUnits { get; set; }
    public int SnapshotReusableUnits { get; set; }
    public List<InitiateDepotClosureTransferItemDto> Items { get; set; } = [];
}

public class InitiateDepotClosureTransferItemDto
{
    public int ItemModelId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int Quantity { get; set; }
}

public class InitiateDepotClosureTransferRemainingItemDto
{
    public int ItemModelId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int CurrentQuantity { get; set; }
    public int AssignedQuantity { get; set; }
    public int RemainingTransferableQuantity { get; set; }
    public int BlockedQuantity { get; set; }
}
