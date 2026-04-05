namespace RESQ.Application.UseCases.Logistics.Commands.ReceiveClosureTransfer;

public class ReceiveClosureTransferResponse
{
    public int TransferId { get; set; }
    public int ClosureId { get; set; }
    public string TransferStatus { get; set; } = string.Empty;
    public int ConsumableUnitsMoved { get; set; }
    public int ReusableItemsMoved { get; set; }
    public DateTime CompletedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
