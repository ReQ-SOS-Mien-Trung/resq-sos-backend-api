namespace RESQ.Application.UseCases.Logistics.Commands.PrepareClosureTransfer;

public class PrepareClosureTransferResponse
{
    public int TransferId { get; init; }
    public string TransferStatus { get; init; } = default!;
    public string Message { get; init; } = default!;
}
