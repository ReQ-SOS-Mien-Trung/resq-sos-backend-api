namespace RESQ.Application.UseCases.Operations.Commands.AssignTeamToMission;

public class AssignTeamToMissionRequestDto
{
    public int RescueTeamId { get; set; }
    public string? TeamType { get; set; }
    public string? Note { get; set; }
}
