using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.CreateMission;

public record CreateMissionCommand(
    int ClusterId,
    string? MissionType,
    double? PriorityScore,
    DateTime? StartTime,
    DateTime? ExpectedEndTime,
    List<CreateActivityItemDto> Activities,
    Guid CreatedById
) : IRequest<CreateMissionResponse>;

public class CreateActivityItemDto
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
