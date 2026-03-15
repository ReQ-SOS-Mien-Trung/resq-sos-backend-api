using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.AddMissionActivity;

public record AddMissionActivityCommand(
    int MissionId,
    int? Step,
    string? ActivityCode,
    string? ActivityType,
    string? Description,
    string? Target,
    string? Items,
    double? TargetLatitude,
    double? TargetLongitude,
    int? RescueTeamId,
    Guid AssignedById
) : IRequest<AddMissionActivityResponse>;
