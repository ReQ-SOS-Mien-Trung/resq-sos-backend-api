namespace RESQ.Application.UseCases.Operations.Commands.AssignTeamToActivity;

public class AssignTeamToActivityResponse
{
    public int ActivityId { get; set; }
    public int MissionTeamId { get; set; }
    public int RescueTeamId { get; set; }
    public string? TeamName { get; set; }
}
