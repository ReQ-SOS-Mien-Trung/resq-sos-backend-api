namespace RESQ.Application.UseCases.Operations.Commands.CompleteMissionTeamExecution;

public class CompleteMissionTeamExecutionResponse
{
    public int MissionId { get; set; }
    public int MissionTeamId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
}