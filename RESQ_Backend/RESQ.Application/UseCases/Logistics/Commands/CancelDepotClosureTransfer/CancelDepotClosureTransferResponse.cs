namespace RESQ.Application.UseCases.Logistics.Commands.CancelDepotClosureTransfer;

public class CancelDepotClosureTransferResponse
{
    public int TransferId { get; set; }
    public int DepotId { get; set; }
    public string TransferStatus { get; set; } = string.Empty;
    public int ClosureId { get; set; }
    public string ClosureStatus { get; set; } = string.Empty;
    public bool RequiresFurtherResolution { get; set; }
    public int RemainingItemCount { get; set; }
    public DateTime CancelledAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
