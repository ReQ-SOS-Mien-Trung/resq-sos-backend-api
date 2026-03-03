namespace RESQ.Application.UseCases.Operations.Commands.UpdateMission;

public class UpdateMissionResponse
{
    public int MissionId { get; set; }
    public string? MissionType { get; set; }
    public double? PriorityScore { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? ExpectedEndTime { get; set; }
}
