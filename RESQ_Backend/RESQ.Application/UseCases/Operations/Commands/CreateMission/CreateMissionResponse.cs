namespace RESQ.Application.UseCases.Operations.Commands.CreateMission;

public class CreateMissionResponse
{
    public int MissionId { get; set; }
    public int ClusterId { get; set; }
    public string? MissionType { get; set; }
    public string? Status { get; set; }
    public int ActivityCount { get; set; }
    public DateTime? CreatedAt { get; set; }
}
