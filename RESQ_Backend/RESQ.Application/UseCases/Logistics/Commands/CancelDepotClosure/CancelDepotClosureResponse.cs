namespace RESQ.Application.UseCases.Logistics.Commands.CancelDepotClosure;

public class CancelDepotClosureResponse
{
    public int ClosureId { get; set; }
    public int DepotId { get; set; }
    public string RestoredStatus { get; set; } = string.Empty;
    public DateTime CancelledAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
