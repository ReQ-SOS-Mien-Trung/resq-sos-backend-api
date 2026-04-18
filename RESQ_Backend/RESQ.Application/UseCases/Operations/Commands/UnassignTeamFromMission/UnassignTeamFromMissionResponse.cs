namespace RESQ.Application.UseCases.Operations.Commands.UnassignTeamFromMission;

public class UnassignTeamFromMissionResponse
{
    public int MissionTeamId { get; set; }
    public string Message { get; set; } = "Đội đã được gỡ khỏi mission.";
}
