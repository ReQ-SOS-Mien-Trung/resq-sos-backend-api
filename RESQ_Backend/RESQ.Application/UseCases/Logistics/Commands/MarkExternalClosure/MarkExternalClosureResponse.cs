namespace RESQ.Application.UseCases.Logistics.Commands.MarkExternalClosure;

public class MarkExternalClosureResponse
{
    public int DepotId { get; set; }
    public int ClosureId { get; set; }
    public string ClosureStatus { get; set; } = string.Empty;
    public string ResolutionType { get; set; } = string.Empty;
    public int RemainingItemCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
