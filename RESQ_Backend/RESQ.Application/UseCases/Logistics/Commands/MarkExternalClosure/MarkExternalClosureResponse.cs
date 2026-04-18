namespace RESQ.Application.UseCases.Logistics.Commands.MarkExternalClosure;

public class MarkExternalClosureResponse
{
    public int DepotId { get; set; }
    public int ClosureId { get; set; }
    public string Message { get; set; } = string.Empty;
}
