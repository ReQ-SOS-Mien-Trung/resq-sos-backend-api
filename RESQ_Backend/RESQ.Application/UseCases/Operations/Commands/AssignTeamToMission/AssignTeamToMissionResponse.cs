namespace RESQ.Application.UseCases.Operations.Commands.AssignTeamToMission;

public class AssignTeamToMissionResponse
{
    public int MissionTeamId { get; set; }
    public int MissionId { get; set; }
    public int RescueTeamId { get; set; }
    public string Status { get; set; } = "Assigned";
    public DateTime AssignedAt { get; set; }
}
