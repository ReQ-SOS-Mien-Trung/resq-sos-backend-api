namespace RESQ.Application.UseCases.Logistics.Commands.CancelDepotClosureTransfer;

public class CancelDepotClosureTransferResponse
{
    public int TransferId { get; set; }
    public int DepotId { get; set; }
    public string TransferStatus { get; set; } = string.Empty;
    public DateTime CancelledAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
