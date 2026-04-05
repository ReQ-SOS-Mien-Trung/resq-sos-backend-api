namespace RESQ.Application.UseCases.Logistics.Commands.CompleteClosureTransfer;

public class CompleteClosureTransferResponse
{
    public int TransferId { get; init; }
    public string TransferStatus { get; init; } = default!;
    public string Message { get; init; } = default!;
}
