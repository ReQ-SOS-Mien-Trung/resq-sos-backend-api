namespace RESQ.Application.UseCases.Personnel.Commands.CancelAssemblyEvent;

public class CancelAssemblyEventResponse
{
    public int EventId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
