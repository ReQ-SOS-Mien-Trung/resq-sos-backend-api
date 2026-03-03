namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionActivity;

public class UpdateMissionActivityResponse
{
    public int ActivityId { get; set; }
    public int? MissionId { get; set; }
    public int? Step { get; set; }
    public string? ActivityType { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
}
