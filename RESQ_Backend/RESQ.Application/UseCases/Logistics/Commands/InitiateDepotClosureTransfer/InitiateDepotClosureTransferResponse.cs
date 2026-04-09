namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosureTransfer;

public class InitiateDepotClosureTransferResponse
{
    public int TransferId { get; set; }
    public int SourceDepotId { get; set; }
    public string SourceDepotName { get; set; } = string.Empty;
    public int TargetDepotId { get; set; }
    public string TargetDepotName { get; set; } = string.Empty;
    public string TransferStatus { get; set; } = string.Empty;
    public int SnapshotConsumableUnits { get; set; }
    public int SnapshotReusableUnits { get; set; }
    public int ReusableItemsSkipped { get; set; }
    public string Message { get; set; } = string.Empty;
}
