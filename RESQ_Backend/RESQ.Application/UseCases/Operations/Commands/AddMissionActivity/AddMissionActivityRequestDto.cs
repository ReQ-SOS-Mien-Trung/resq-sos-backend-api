namespace RESQ.Application.UseCases.Operations.Commands.AddMissionActivity;

public class AddMissionActivityRequestDto
{
    public int? Step { get; set; }
    public string? ActivityCode { get; set; }
    public string? ActivityType { get; set; }
    public string? Description { get; set; }
    public string? Target { get; set; }
    public string? Items { get; set; }
    public double? TargetLatitude { get; set; }
    public double? TargetLongitude { get; set; }
}
