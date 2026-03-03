namespace RESQ.Application.UseCases.Operations.Commands.AddMissionActivity;

public class AddMissionActivityResponse
{
    public int ActivityId { get; set; }
    public int MissionId { get; set; }
    public int? Step { get; set; }
    public string? ActivityType { get; set; }
    public string? Status { get; set; }
}
