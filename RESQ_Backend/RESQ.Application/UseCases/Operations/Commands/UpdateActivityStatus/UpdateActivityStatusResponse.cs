namespace RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;

public class UpdateActivityStatusResponse
{
    public int ActivityId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid DecisionBy { get; set; }
}
