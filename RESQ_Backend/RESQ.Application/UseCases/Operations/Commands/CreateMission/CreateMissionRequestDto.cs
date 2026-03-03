namespace RESQ.Application.UseCases.Operations.Commands.CreateMission;

public class CreateMissionRequestDto
{
    public int ClusterId { get; set; }
    public string? MissionType { get; set; }
    public double? PriorityScore { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? ExpectedEndTime { get; set; }
    public List<CreateActivityItemDto> Activities { get; set; } = [];
}
