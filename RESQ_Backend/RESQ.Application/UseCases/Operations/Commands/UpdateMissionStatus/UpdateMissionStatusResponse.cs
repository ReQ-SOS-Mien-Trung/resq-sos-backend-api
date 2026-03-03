namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionStatus;

public class UpdateMissionStatusResponse
{
    public int MissionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}
