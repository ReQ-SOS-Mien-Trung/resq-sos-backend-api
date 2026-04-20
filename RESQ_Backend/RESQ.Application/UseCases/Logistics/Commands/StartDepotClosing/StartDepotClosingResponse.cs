namespace RESQ.Application.UseCases.Logistics.Commands.StartDepotClosing;

public class StartDepotClosingResponse
{
    public int DepotId { get; set; }
    public int ClosureId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
