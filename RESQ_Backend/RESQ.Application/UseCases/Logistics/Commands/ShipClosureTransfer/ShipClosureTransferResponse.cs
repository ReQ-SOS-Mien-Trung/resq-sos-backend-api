namespace RESQ.Application.UseCases.Logistics.Commands.ShipClosureTransfer;

public class ShipClosureTransferResponse
{
    public int TransferId { get; set; }
    public string TransferStatus { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
