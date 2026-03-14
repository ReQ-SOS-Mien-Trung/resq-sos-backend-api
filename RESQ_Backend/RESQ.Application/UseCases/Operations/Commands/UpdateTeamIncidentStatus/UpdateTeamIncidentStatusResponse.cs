namespace RESQ.Application.UseCases.Operations.Commands.UpdateTeamIncidentStatus;

public class UpdateTeamIncidentStatusResponse
{
    public int IncidentId { get; set; }
    public string Status { get; set; } = string.Empty;
}
