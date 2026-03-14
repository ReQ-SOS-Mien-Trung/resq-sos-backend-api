namespace RESQ.Application.UseCases.Emergency.Commands.CancelSosRequest;

public class CancelSosRequestResponse
{
    public int SosRequestId { get; set; }
    public string Status { get; set; } = "Cancelled";
}
