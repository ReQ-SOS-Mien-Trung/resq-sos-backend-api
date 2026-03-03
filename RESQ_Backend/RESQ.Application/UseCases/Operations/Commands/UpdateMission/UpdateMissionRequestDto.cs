namespace RESQ.Application.UseCases.Operations.Commands.UpdateMission;

public class UpdateMissionRequestDto
{
    public string? MissionType { get; set; }
    public double? PriorityScore { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? ExpectedEndTime { get; set; }
}
